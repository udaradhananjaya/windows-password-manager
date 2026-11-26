using System.Windows;
namespace HelloAutofill;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase));
        MainWindow = window;
        window.Show();
    }
}

