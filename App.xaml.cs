using System.Windows;
using DesksideHub.Platform;

namespace LfpHub;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        HubPlatform.Configure(
            new WindowsPathProvider(),
            new DefaultNodeLocator(),
            new WindowsOsShell());

        base.OnStartup(e);

        AppConfig.EnsureWorkingTemplates();

        if (AppConfig.NeedsFirstRunSetup())
        {
            var setup = new SetupWindow();
            var ok = setup.ShowDialog() == true;
            if (!ok || AppConfig.NeedsFirstRunSetup())
            {
                Shutdown(1);
                return;
            }
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
}
