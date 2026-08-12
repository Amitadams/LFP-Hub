using System.Windows;

namespace LfpHub;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        // Blank identity only — never seed another tech's name.
        TxtDisplayName.Text = "";
        TxtUsername.Text = "";
        TxtEmail.Text = "";
        TxtSite.Text = "GFNV";
        TxtSignatureTitle.Text = "Tesla IT Support — GFNV";
        TxtWalkupHours.Text = "7:00 AM – 7:00 PM";
        TxtDisplayName.Focus();
    }

    void BtnContinue_Click(object sender, RoutedEventArgs e)
    {
        ErrText.Text = "";
        var tech = new TechIdentity
        {
            DisplayName = TxtDisplayName.Text.Trim(),
            Username = TxtUsername.Text.Trim(),
            Email = TxtEmail.Text.Trim(),
            Site = string.IsNullOrWhiteSpace(TxtSite.Text) ? "GFNV" : TxtSite.Text.Trim(),
            SignatureTitle = TxtSignatureTitle.Text.Trim(),
            WalkupHours = TxtWalkupHours.Text.Trim(),
        };

        var err = tech.ValidationError();
        if (!string.IsNullOrEmpty(err))
        {
            ErrText.Text = err;
            return;
        }

        try
        {
            AppConfig.EnsureWorkingTemplates();
            var cfg = AppConfig.Load();
            cfg.Tech = tech;
            cfg.SetupComplete = true;
            cfg.Save();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrText.Text = ex.Message;
        }
    }
}
