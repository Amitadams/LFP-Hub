using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LfpHub;

public partial class SettingsWindow : Window
{
    readonly Dictionary<string, string> _drafts = new(StringComparer.OrdinalIgnoreCase);
    string? _currentFile;
    bool _dirty;
    bool _loading;
    bool _suppressThemePreview;
    readonly string _themeWhenOpened;
    string _selectedTheme;
    bool _themeSaved;

    public SettingsWindow()
    {
        InitializeComponent();
        _loading = true;
        var cfg = AppConfig.Load();
        _themeWhenOpened = LfpThemes.Normalize(cfg.Theme);
        _selectedTheme = _themeWhenOpened;
        Closing += SettingsWindow_Closing;

        // Never show another tech's identity; incomplete setup stays blank.
        if (cfg.Tech.IsConfigured)
        {
            TxtDisplayName.Text = cfg.Tech.DisplayName;
            TxtTechUsername.Text = cfg.Tech.Username;
            TxtEmail.Text = cfg.Tech.Email;
            TxtSite.Text = string.IsNullOrWhiteSpace(cfg.Tech.Site) ? "GFNV" : cfg.Tech.Site;
            TxtSignatureTitle.Text = cfg.Tech.SignatureTitle;
            TxtWalkupHours.Text = string.IsNullOrWhiteSpace(cfg.Tech.WalkupHours)
                ? "7:00 AM - 7:00 PM"
                : cfg.Tech.WalkupHours;
        }
        else
        {
            TxtDisplayName.Text = "";
            TxtTechUsername.Text = "";
            TxtEmail.Text = "";
            TxtSite.Text = "GFNV";
            TxtSignatureTitle.Text = "Tesla IT Support - GFNV";
            TxtWalkupHours.Text = "7:00 AM - 7:00 PM";
        }

        var dir = AppConfig.ResolveTemplatesDir(cfg);
        TxtTemplatesDir.Text = dir;
        ReloadList(dir);
        BuildThemeRadios();
        _loading = false;
        _dirty = false;
        StatusText.Text = cfg.Tech.IsConfigured ? "Ready" : "Set your tech identity, then Save.";
    }

    void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Revert live preview if theme was changed but not saved
        if (!_themeSaved
            && !string.Equals(LfpThemes.Current.Id, _themeWhenOpened, StringComparison.OrdinalIgnoreCase))
        {
            ThemeService.Apply(_themeWhenOpened);
        }
    }

    void BuildThemeRadios()
    {
        ThemeRadios.Children.Clear();
        _suppressThemePreview = true;
        foreach (var theme in LfpThemes.All)
        {
            var radio = new RadioButton
            {
                Content = theme.Title,
                Tag = theme.Id,
                GroupName = "LfpTheme",
                Margin = new Thickness(0, 0, 0, 8),
                IsChecked = theme.Id.Equals(_selectedTheme, StringComparison.OrdinalIgnoreCase),
            };
            radio.Checked += ThemeRadio_Changed;
            ThemeRadios.Children.Add(radio);
        }
        TxtThemeBlurb.Text = LfpThemes.Resolve(_selectedTheme).Blurb;
        _suppressThemePreview = false;
    }

    void ThemeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressThemePreview) return;
        if (sender is not RadioButton { IsChecked: true, Tag: string id }) return;
        _selectedTheme = LfpThemes.Normalize(id);
        TxtThemeBlurb.Text = LfpThemes.Resolve(_selectedTheme).Blurb;
        ThemeService.Apply(_selectedTheme);
        _dirty = true;
    }

    void Field_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _dirty = true;
    }

    void TemplateBody_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _dirty = true;
    }

    void ReloadList(string dir)
    {
        FlushCurrent();
        TemplateList.Items.Clear();
        _drafts.Clear();
        _currentFile = null;
        TxtTemplateBody.Text = "";

        foreach (var name in TemplateFiles.AllNames)
        {
            var path = Path.Combine(dir, name);
            var body = File.Exists(path) ? File.ReadAllText(path) : "";
            _drafts[name] = body;
            TemplateList.Items.Add(new TemplateListItem(name, TemplateFiles.Label(name)));
        }

        if (TemplateList.Items.Count > 0)
            TemplateList.SelectedIndex = 0;
    }

    void FlushCurrent()
    {
        if (_currentFile is null) return;
        _drafts[_currentFile] = TxtTemplateBody.Text;
    }

    void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FlushCurrent();
        if (TemplateList.SelectedItem is not TemplateListItem item)
            return;
        _loading = true;
        _currentFile = item.FileName;
        TxtTemplateBody.Text = _drafts.TryGetValue(item.FileName, out var body) ? body : "";
        _loading = false;
    }

    void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select templates folder",
            InitialDirectory = TxtTemplatesDir.Text,
        };
        if (dlg.ShowDialog(this) != true) return;

        FlushCurrent();
        TxtTemplatesDir.Text = dlg.FolderName;
        foreach (var name in TemplateFiles.AllNames)
        {
            var path = Path.Combine(dlg.FolderName, name);
            if (!File.Exists(path))
            {
                var bundled = Path.Combine(AppConfig.BundledTemplatesDir, name);
                if (File.Exists(bundled))
                    File.Copy(bundled, path, overwrite: false);
            }
        }
        ReloadList(dlg.FolderName);
        _dirty = true;
        StatusText.Text = "Folder updated - Save to keep.";
    }

    void BtnResetDir_Click(object sender, RoutedEventArgs e)
    {
        AppConfig.EnsureWorkingTemplates();
        TxtTemplatesDir.Text = AppConfig.DefaultWorkingTemplatesDir;
        ReloadList(AppConfig.DefaultWorkingTemplatesDir);
        _dirty = true;
        StatusText.Text = "Using default folder - Save to keep.";
    }

    TechIdentity ReadTechFromUi() => new()
    {
        DisplayName = TxtDisplayName.Text.Trim(),
        Username = TxtTechUsername.Text.Trim(),
        Email = TxtEmail.Text.Trim(),
        Site = string.IsNullOrWhiteSpace(TxtSite.Text) ? "GFNV" : TxtSite.Text.Trim(),
        SignatureTitle = TxtSignatureTitle.Text.Trim(),
        WalkupHours = TxtWalkupHours.Text.Trim(),
    };

    void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        FlushCurrent();
        var tech = ReadTechFromUi();
        var techErr = tech.ValidationError();
        if (!string.IsNullOrEmpty(techErr))
        {
            MessageBox.Show(this, techErr, "Tech identity",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dir = TxtTemplatesDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(dir))
        {
            MessageBox.Show(this, "Templates folder is empty.", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            foreach (var name in TemplateFiles.AllNames)
            {
                var body = _drafts.TryGetValue(name, out var t) ? t : "";
                File.WriteAllText(Path.Combine(dir, name), body);
            }

            var cfg = AppConfig.Load();
            cfg.Tech = tech;
            cfg.SetupComplete = true;
            cfg.Theme = LfpThemes.Normalize(_selectedTheme);
            cfg.TemplatesDirectory =
                string.Equals(dir, AppConfig.DefaultWorkingTemplatesDir, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : dir;
            cfg.Save();
            ThemeService.Apply(cfg.Theme);
            _selectedTheme = cfg.Theme;
            _themeSaved = true;

            _dirty = false;
            StatusText.Text = "Saved.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        FlushCurrent();
        if (_dirty)
        {
            var r = MessageBox.Show(this, "Discard unsaved changes?", "Settings",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (r != MessageBoxResult.Yes) return;
            ThemeService.Apply(_themeWhenOpened);
            _themeSaved = true; // prevent Closing from double-applying
        }
        else
        {
            _themeSaved = true;
        }
        DialogResult = true;
        Close();
    }

    sealed class TemplateListItem(string fileName, string label)
    {
        public string FileName { get; } = fileName;
        public override string ToString() => label;
    }
}
