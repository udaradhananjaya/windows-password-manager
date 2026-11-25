using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;

namespace HelloAutofill;

static class AppInfo
{
    public static string Version => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.1";
    public const string ReleasesUrl = "https://github.com/udaradhananjaya/windows-password-manager/releases";
}

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + AppInfo.Version;
        SourceInitialized += (_, _) => { int dark = 1; Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); };
    }
    private void OpenReleases_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(AppInfo.ReleasesUrl) { UseShellExecute = true }); }
        catch (Exception ex) { System.Windows.MessageBox.Show("Could not open the release page: " + ex.Message, "Hello Autofill"); }
    }
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        UpdateStatus.Text = "Checking for updates...";
        try
        {
            var update = await UpdateChecker.CheckAsync();
            UpdateStatus.Text = update.IsNewer ? $"Version {update.Tag} is available. Open releases to download it." : $"You have the latest release ({update.Tag}).";
        }
        catch (Exception ex) when (UpdateChecker.IsCheckFailure(ex))
        {
            UpdateStatus.Text = "Could not check GitHub. Please try again later or open the release page.";
        }
        finally { CheckButton.IsEnabled = true; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
