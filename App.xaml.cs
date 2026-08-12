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
    }
}
