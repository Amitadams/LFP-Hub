using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using DesksideHub.Models;
using DesksideHub.Platform;
using DesksideHub.Services;

namespace LfpHub;

public partial class MainWindow : Window
{
    readonly JobRunner _runner = new();
    readonly string? _nodeExe;
    readonly string? _skillsRoot;

    public MainWindow()
    {
        InitializeComponent();
        AppConfig.EnsureWorkingTemplates();

        _nodeExe = HubPlatform.Nodes.FindNodeExe();
        _skillsRoot = HubPlatform.Nodes.FindSkillsRoot();
        _runner.OutputReceived += line => Dispatcher.Invoke(() => AppendLog(RedactSecrets(line)));
        _runner.StatusChanged += msg => Dispatcher.Invoke(() => StatusText.Text = msg);

        RefreshStatusReady();
        UpdateTempVisibility();
        TxtUsername.Focus();
    }

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
        else
            StatusText.Text = $"Ready · {cfg.Tech.DisplayName}";
    }

    void Mode_Changed(object sender, RoutedEventArgs e) => UpdateTempVisibility();

    void UpdateTempVisibility()
    {
        if (TempPanel is null) return;
        TempPanel.Visibility = RadMfa?.IsChecked == true
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    void BtnClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow { Owner = this };
        win.ShowDialog();
        RefreshStatusReady();
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

    void BtnStop_Click(object sender, RoutedEventArgs e) => _runner.Cancel();

    static string SelectedMode(bool setup, bool mfa) =>
        setup ? "setup" : mfa ? "mfa" : "password";

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
        if (!Directory.Exists(Path.Combine(_skillsRoot, "lfp-reset")))
        {
            ErrText.Text = "lfp-reset skill not found.";
            return;
        }

        var cfg = AppConfig.Load();
        if (!cfg.Tech.IsConfigured)
        {
            ErrText.Text = "Set your tech identity in Settings first.";
            return;
        }

        AppConfig.WriteTechIdentityFile(cfg.Tech);

        var username = TxtUsername.Text.Trim();
        var ita = TxtIta.Text.Trim();
        var name = TxtName.Text.Trim();
        var badge = TxtBadge.Text.Trim();
        var temp = TxtTemp.Password.Trim();
        var mode = SelectedMode(RadSetup.IsChecked == true, RadMfa.IsChecked == true);
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
        if (mode is "password" or "setup")
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
            var ok = MessageBox.Show(
                this,
                "This will send Teams, comment, and close the ITA (unless skipped).\n\nContinue?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes)
                return;
        }

        var args = new List<string>
        {
            $"--{mode}",
            "--templates-dir",
            templatesDir,
            "--tech-identity",
            AppConfig.TechIdentityPath,
        };
        if (!string.IsNullOrWhiteSpace(temp) && mode is not "mfa")
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
            BaseArgs = args.ToArray(),
        };

        BtnRun.IsEnabled = false;
        BtnStop.IsEnabled = true;
        AppendLog(dry ? "\n── dry-run ──\n" : "\n── live ──\n");
        try
        {
            var result = await _runner.RunAsync(
                job,
                _skillsRoot,
                _nodeExe,
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
