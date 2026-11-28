using System.Windows;
using System.Windows.Input;
namespace HelloAutofill;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new System.Windows.Input.KeyEventHandler(CloseOnEscape));
        var window = new MainWindow(e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase));
        MainWindow = window;
        window.Show();
    }
    private static void CloseOnEscape(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Window window) return;
        e.Handled = true;
        window.Close();
    }
}

