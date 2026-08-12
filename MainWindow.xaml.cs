using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using DesksideHub.Models;
using DesksideHub.Platform;
using DesksideHub.Services;
using Microsoft.Win32;

namespace LfpHub;

public partial class MainWindow : Window
{
    readonly JobRunner _runner = new();
    readonly string? _nodeExe;
    readonly string? _skillsRoot;
    List<BulkCloseoutRow> _bulkRows = [];
    bool _bulkCancel;

    public MainWindow()
    {
        InitializeComponent();
        AppConfig.EnsureWorkingTemplates();

        _nodeExe = HubPlatform.Nodes.FindNodeExe();
        _skillsRoot = HubPlatform.Nodes.FindSkillsRoot();
        _runner.OutputReceived += line => Dispatcher.Invoke(() => AppendLog(RedactSecrets(line)));
        _runner.StatusChanged += msg => Dispatcher.Invoke(() => StatusText.Text = msg);

        InitBulkDefaultTypeCombo();
        RefreshStatusReady();
        UpdateJobUi();
        TxtUsername.Focus();
    }

    void InitBulkDefaultTypeCombo()
    {
        if (CmbBulkDefaultType is null) return;
        CmbBulkDefaultType.Items.Clear();
        CmbBulkDefaultType.Items.Add(new BulkTypeItem("password", "Password"));
        CmbBulkDefaultType.Items.Add(new BulkTypeItem("setup", "First login"));
        CmbBulkDefaultType.Items.Add(new BulkTypeItem("mfa", "MFA"));
        CmbBulkDefaultType.Items.Add(new BulkTypeItem("noaccount", "No account"));
        CmbBulkDefaultType.SelectedIndex = 0;
        CmbBulkDefaultType.DisplayMemberPath = nameof(BulkTypeItem.Label);
    }

    sealed class BulkTypeItem(string mode, string label)
    {
        public string Mode { get; } = mode;
        public string Label { get; } = label;
        public override string ToString() => Label;
    }

    string BulkDefaultMode()
    {
        if (CmbBulkDefaultType?.SelectedItem is BulkTypeItem item)
            return item.Mode;
        return CurrentCloseoutMode();
    }

    bool IsOpenTicketJob => RadJobOpenTicket?.IsChecked == true;
    bool IsBulkJob => RadJobBulk?.IsChecked == true;

    void RefreshStatusReady()
    {
        var cfg = AppConfig.Load();
        if (!cfg.Tech.IsConfigured)
        {
            StatusText.Text = "Set your tech identity in Settings.";
            return;
        }
        if (_nodeExe is null || _skillsRoot is null)
            StatusText.Text = "Node or skills not found.";
        else if (!Directory.Exists(Path.Combine(_skillsRoot, "lfp-reset")))
            StatusText.Text = "lfp-reset skill missing.";
        else if (!Directory.Exists(Path.Combine(_skillsRoot, "lfp-open-ticket")))
            StatusText.Text = $"Ready · {cfg.Tech.DisplayName} (open LFP ticket skill missing)";
        else
            StatusText.Text = $"Ready · {cfg.Tech.DisplayName}";
    }

    void Job_Changed(object sender, RoutedEventArgs e) => UpdateJobUi();

    void Mode_Changed(object sender, RoutedEventArgs e) => UpdateJobUi();

    void UpdateJobUi()
    {
        // Radio Checked fires during InitializeComponent before later named fields exist.
        if (CloseoutTypePanel is null
            || OpenTicketPanel is null
            || CloseoutOptionsPanel is null
            || OpenTicketOptionsPanel is null
            || TempPanel is null
            || BulkPanel is null
            || SingleIdentityCard is null)
            return;

        var openTicket = IsOpenTicketJob;
        var bulk = IsBulkJob;

        SingleIdentityCard.Visibility = bulk ? Visibility.Collapsed : Visibility.Visible;
        BulkPanel.Visibility = bulk ? Visibility.Visible : Visibility.Collapsed;
        OpenTicketPanel.Visibility = openTicket ? Visibility.Visible : Visibility.Collapsed;
        OpenTicketOptionsPanel.Visibility = openTicket ? Visibility.Visible : Visibility.Collapsed;

        // Type radios only for single close-out (bulk uses Default type + per-row Ticket Type)
        CloseoutTypePanel.Visibility = (!openTicket && !bulk) ? Visibility.Visible : Visibility.Collapsed;
        CloseoutOptionsPanel.Visibility = (!openTicket) ? Visibility.Visible : Visibility.Collapsed;

        if (openTicket || bulk)
        {
            // Bulk: OTP comes from each row — hide single OTP field
            TempPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            TempPanel.Visibility = NeedsTempForCurrentMode()
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (bulk && BulkParseStatus is not null && _bulkRows.Count == 0
            && (string.IsNullOrWhiteSpace(BulkParseStatus.Text)
                || BulkParseStatus.Text.StartsWith("Paste", StringComparison.Ordinal)))
            BulkParseStatus.Text = "Paste CSV with headers, or Sample → Parse.";
    }

    bool NeedsTempForCurrentMode() =>
        TemplateFiles.NeedsTempPassword(CurrentCloseoutMode());

    void BtnClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow { Owner = this };
        win.ShowDialog();
        RefreshStatusReady();
    }

    void BtnBulkSample_Click(object sender, RoutedEventArgs e)
    {
        TxtBulk.Text = BulkCloseoutParser.SampleCsv;
        ApplyBulkParse(BulkCloseoutParser.Parse(TxtBulk.Text, BulkDefaultMode()));
    }

    void BtnLoadCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Load bulk close-out list",
            Filter = "CSV or text (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            TxtBulk.Text = File.ReadAllText(dlg.FileName);
            ApplyBulkParse(BulkCloseoutParser.Parse(TxtBulk.Text, BulkDefaultMode()));
        }
        catch (Exception ex)
        {
            ErrText.Text = ex.Message;
        }
    }

    void BtnParseBulk_Click(object sender, RoutedEventArgs e) =>
        ApplyBulkParse(BulkCloseoutParser.Parse(TxtBulk.Text, BulkDefaultMode()));

    void ApplyBulkParse(BulkParseResult parsed)
    {
        _bulkRows = parsed.Rows;
        foreach (var err in parsed.Errors)
            AppendLog("parse: " + err + "\n");

        if (_bulkRows.Count == 0)
        {
            BulkParseStatus.Text = parsed.Errors.Count > 0
                ? $"{parsed.Errors.Count} error(s), 0 rows"
                : "No rows found";
            ErrText.Text = parsed.Errors.Count > 0 ? parsed.Errors[0] : "No bulk rows to run.";
            return;
        }

        ErrText.Text = "";
        var byType = _bulkRows.GroupBy(r => r.ModeLabel)
            .Select(g => $"{g.Count()} {g.Key}")
            .ToList();
        var summary = string.Join(" · ", byType);
        BulkParseStatus.Text = parsed.Errors.Count > 0
            ? $"{_bulkRows.Count} ready ({summary}) · {parsed.Errors.Count} skipped"
            : $"{_bulkRows.Count} ready · {summary}";
        if (parsed.HadHeader && !string.IsNullOrEmpty(parsed.DetectedColumns))
            AppendLog($"\n── headers: {parsed.DetectedColumns} ──\n");
        AppendLog($"── bulk parse: {_bulkRows.Count} row(s) ──\n");
        foreach (var r in _bulkRows)
        {
            var extra = string.IsNullOrEmpty(r.Name) ? "" : $" · {r.Name}";
            AppendLog($"  {r.Username} · {r.ModeLabel}{extra}\n");
        }
    }

    void AppendLog(string line)
    {
        LogBox.AppendText(line);
        LogBox.ScrollToEnd();
    }

    static string RedactSecrets(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        return Regex.Replace(
            line,
            @"(--(?:temp|otp|007|link)(?:=|\s+))(\S+)",
            "$1***",
            RegexOptions.IgnoreCase);
    }

    void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _bulkCancel = true;
        _runner.Cancel();
    }

    string CurrentCloseoutMode()
    {
        if (RadNoAccount?.IsChecked == true) return "noaccount";
        if (RadSetup?.IsChecked == true) return "setup";
        if (RadMfa?.IsChecked == true) return "mfa";
        return "password";
    }

    async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        ErrText.Text = "";
        if (_runner.IsRunning)
        {
            MessageBox.Show(this, "Already running.", "LFP Hub",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_nodeExe is null || _skillsRoot is null)
        {
            ErrText.Text = "Node or skills path missing.";
            return;
        }

        var cfg = AppConfig.Load();
        if (!cfg.Tech.IsConfigured)
        {
            ErrText.Text = "Set your tech identity in Settings first.";
            return;
        }

        AppConfig.WriteTechIdentityFile(cfg.Tech);

        if (IsBulkJob)
            await RunBulkCloseoutAsync(cfg);
        else if (IsOpenTicketJob)
            await RunOpenTicketAsync(cfg);
        else
            await RunCloseoutAsync(cfg);
    }

    async Task RunBulkCloseoutAsync(AppConfig cfg)
    {
        if (!Directory.Exists(Path.Combine(_skillsRoot!, "lfp-reset")))
        {
            ErrText.Text = "lfp-reset skill not found.";
            return;
        }

        // Re-parse so edits since last Parse are included
        var parsed = BulkCloseoutParser.Parse(TxtBulk.Text, BulkDefaultMode());
        ApplyBulkParse(parsed);
        if (_bulkRows.Count == 0)
            return;

        var templatesDir = AppConfig.ResolveTemplatesDir(cfg);
        foreach (var mode in _bulkRows.Select(r => r.Mode).Distinct())
        {
            var templateFile = Path.Combine(templatesDir, TemplateFiles.ForMode(mode));
            if (!File.Exists(templateFile))
            {
                ErrText.Text = $"Template missing: {Path.GetFileName(templateFile)}. Open Settings.";
                return;
            }
        }

        var dry = ChkDryRun.IsChecked == true;
        if (!dry)
        {
            var ok = MessageBox.Show(
                this,
                $"LIVE bulk close-out for {_bulkRows.Count} row(s).\n\nTeams / comment / close each (unless skipped).\n\nContinue?",
                "Confirm bulk",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes)
                return;
        }

        _bulkCancel = false;
        BtnRun.IsEnabled = false;
        BtnStop.IsEnabled = true;
        var okCount = 0;
        var failCount = 0;
        AppendLog(dry
            ? $"\n── bulk dry-run · {_bulkRows.Count} ──\n"
            : $"\n── bulk LIVE · {_bulkRows.Count} ──\n");

        try
        {
            for (var i = 0; i < _bulkRows.Count; i++)
            {
                if (_bulkCancel)
                {
                    AppendLog("\n── bulk cancelled ──\n");
                    StatusText.Text = $"Bulk cancelled · {okCount} ok · {failCount} failed";
                    break;
                }

                var row = _bulkRows[i];
                AppendLog($"\n── [{i + 1}/{_bulkRows.Count}] {row.Username} · {row.ModeLabel} ──\n");
                StatusText.Text = $"Bulk {i + 1}/{_bulkRows.Count} · {row.Username} · {row.ModeLabel}";

                var args = BuildCloseoutArgs(
                    row.Mode,
                    templatesDir,
                    username: row.Username,
                    ita: "",
                    name: row.Name,
                    badge: row.Badge,
                    temp: TemplateFiles.NeedsTempPassword(row.Mode) ? row.TempPassword : "");

                var job = new HubJob
                {
                    Id = "lfp-reset",
                    Title = $"Bulk · {row.Username} · {row.ModeLabel}",
                    Description = "TeslaLFP close-out",
                    SkillFolder = "lfp-reset",
                    Script = "lfp-reset.mjs",
                    SupportsDryRun = true,
                    MutatesLive = true,
                    RequiresVault = true,
                    BaseArgs = args,
                };

                try
                {
                    var result = await _runner.RunAsync(
                        job,
                        _skillsRoot!,
                        _nodeExe!,
                        new JobRunOptions
                        {
                            DryRun = dry,
                            // Prefer headless for bulk unless user wants visible SSO
                            VisibleBrowser = ChkVisible.IsChecked == true,
                        });
                    AppendLog($"── {row.Username} exit {result.ExitCode} ──\n");
                    if (result.Success) okCount++;
                    else failCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    AppendLog($"ERROR {row.Username}: {ex.Message}\n");
                }
            }

            if (!_bulkCancel)
            {
                AppendLog($"\n── bulk done · {okCount} ok · {failCount} failed ──\n");
                StatusText.Text = $"Bulk done · {okCount} ok · {failCount} failed";
            }
        }
        finally
        {
            BtnRun.IsEnabled = true;
            BtnStop.IsEnabled = false;
            _bulkCancel = false;
        }
    }

    string[] BuildCloseoutArgs(
        string mode,
        string templatesDir,
        string username,
        string ita,
        string name,
        string badge,
        string temp)
    {
        var args = new List<string>
        {
            $"--{mode}",
            "--templates-dir",
            templatesDir,
            "--tech-identity",
            AppConfig.TechIdentityPath,
        };
        if (!string.IsNullOrWhiteSpace(temp) && TemplateFiles.NeedsTempPassword(mode))
        {
            args.Add("--temp");
            args.Add(temp);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            args.Add("--name");
            args.Add(name);
        }
        if (!string.IsNullOrWhiteSpace(badge))
        {
            args.Add("--badge");
            args.Add(badge);
        }
        if (ChkNoTeams.IsChecked == true)
            args.Add("--no-teams");
        if (ChkNoClose.IsChecked == true)
            args.Add("--no-close");
        if (!string.IsNullOrWhiteSpace(ita))
            args.Add(ita);
        if (!string.IsNullOrWhiteSpace(username))
            args.Add(username);
        return args.ToArray();
    }

    async Task RunOpenTicketAsync(AppConfig cfg)
    {
        var skillDir = Path.Combine(_skillsRoot!, "lfp-open-ticket");
        if (!Directory.Exists(skillDir))
        {
            ErrText.Text = "lfp-open-ticket skill not found under skills root.";
            return;
        }

        var username = TxtUsername.Text.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            ErrText.Text = "Username is required to open an LFP ticket.";
            return;
        }

        var dry = ChkDryRun.IsChecked == true;
        if (!dry)
        {
            var ok = MessageBox.Show(
                this,
                "Create an open LFP ITA (component MFA - Reset) and open it?\n\nContinue?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);
            if (ok != MessageBoxResult.Yes)
                return;
        }

        var args = new List<string>
        {
            "--tech-identity",
            AppConfig.TechIdentityPath,
            username,
        };
        var name = TxtName.Text.Trim();
        var badge = TxtBadge.Text.Trim();
        var notes = TxtNotes.Text.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            args.Add("--name");
            args.Add(name);
        }
        if (!string.IsNullOrWhiteSpace(badge))
        {
            args.Add("--badge");
            args.Add(badge);
        }
        if (!string.IsNullOrWhiteSpace(notes))
        {
            args.Add("--notes");
            args.Add(notes);
        }
        if (ChkNoOpen.IsChecked == true)
            args.Add("--no-open");

        var job = new HubJob
        {
            Id = "lfp-open-ticket",
            Title = "Open LFP Ticket",
            Description = "Create open ITA with MFA - Reset",
            SkillFolder = "lfp-open-ticket",
            Script = "lfp-open-ticket.mjs",
            SupportsDryRun = true,
            MutatesLive = true,
            RequiresVault = false,
            BaseArgs = args.ToArray(),
        };

        await ExecuteJobAsync(job, dry);
    }

    async Task RunCloseoutAsync(AppConfig cfg)
    {
        if (!Directory.Exists(Path.Combine(_skillsRoot!, "lfp-reset")))
        {
            ErrText.Text = "lfp-reset skill not found.";
            return;
        }

        var username = TxtUsername.Text.Trim();
        var ita = TxtIta.Text.Trim();
        var name = TxtName.Text.Trim();
        var badge = TxtBadge.Text.Trim();
        var temp = TxtTemp.Password.Trim();
        var mode = CurrentCloseoutMode();
        var templatesDir = AppConfig.ResolveTemplatesDir(cfg);
        var templateFile = Path.Combine(templatesDir, TemplateFiles.ForMode(mode));

        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(ita))
        {
            ErrText.Text = "Enter a username or ITA.";
            return;
        }
        if (!File.Exists(templateFile))
        {
            ErrText.Text = $"Template missing: {Path.GetFileName(templateFile)}. Open Settings.";
            return;
        }
        if (TemplateFiles.NeedsTempPassword(mode))
        {
            if (string.IsNullOrWhiteSpace(temp) || temp.Length < 8)
            {
                ErrText.Text = "One-time password required (8+ characters).";
                return;
            }
            if (temp.Contains("007.teslamotors.com", StringComparison.OrdinalIgnoreCase))
            {
                ErrText.Text = "Use the one-time password, not a 007 link.";
                return;
            }
        }

        var dry = ChkDryRun.IsChecked == true;
        if (!dry)
        {
            var confirmBody = mode is "noaccount"
                ? "This will Teams the no-account referral, comment, and close the ITA (unless skipped).\n\nContinue?"
                : "This will send Teams, comment, and close the ITA (unless skipped).\n\nContinue?";
            var ok = MessageBox.Show(
                this,
                confirmBody,
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes)
                return;
        }

        var args = BuildCloseoutArgs(
            mode,
            templatesDir,
            username,
            ita,
            name,
            badge,
            TemplateFiles.NeedsTempPassword(mode) ? temp : "");
        var job = new HubJob
        {
            Id = "lfp-reset",
            Title = "LFP Hub",
            Description = "TeslaLFP close-out",
            SkillFolder = "lfp-reset",
            Script = "lfp-reset.mjs",
            SupportsDryRun = true,
            MutatesLive = true,
            RequiresVault = true,
            BaseArgs = args,
        };

        await ExecuteJobAsync(job, dry);
    }

    async Task ExecuteJobAsync(HubJob job, bool dry)
    {
        BtnRun.IsEnabled = false;
        BtnStop.IsEnabled = true;
        AppendLog(dry ? "\n── dry-run ──\n" : "\n── live ──\n");
        try
        {
            var result = await _runner.RunAsync(
                job,
                _skillsRoot!,
                _nodeExe!,
                new JobRunOptions
                {
                    DryRun = dry,
                    VisibleBrowser = ChkVisible.IsChecked == true,
                });
            AppendLog($"\n── exit {result.ExitCode} in {result.Duration:mm\\:ss} ──\n");
            StatusText.Text = result.Success
                ? $"Done ({result.Duration:mm\\:ss})"
                : $"Failed (exit {result.ExitCode})";
        }
        catch (Exception ex)
        {
            ErrText.Text = ex.Message;
            AppendLog("\nERROR: " + ex.Message + "\n");
            StatusText.Text = "Error";
        }
        finally
        {
            BtnRun.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }
    }
}
