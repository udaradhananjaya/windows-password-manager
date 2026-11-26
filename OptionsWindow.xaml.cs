using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace HelloAutofill;

public partial class OptionsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HelloAutofill";
    private bool initialized;

    public OptionsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => { int dark = 1; Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); };
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); StartWithWindows.IsChecked = key?.GetValue(ValueName) is string; }
        catch (Exception ex) { StartWithWindows.IsEnabled = false; Status.Text = "Could not read startup setting: " + ex.Message; }
        initialized = true;
    }
    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true) ?? throw new InvalidOperationException("Could not open startup settings.");
            if (StartWithWindows.IsChecked == true)
            {
                string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Could not find the app executable.");
                string command = $"\"{executable}\"";
                if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    command += $" \"{typeof(App).Assembly.Location}\"";
                command += " --startup";
                if (command.Length > 260) throw new InvalidOperationException("The app path is too long for Windows startup.");
                key.SetValue(ValueName, command, RegistryValueKind.String);
                Status.Text = "Enabled for your Windows account.";
            }
            else { key.DeleteValue(ValueName, false); Status.Text = "Windows startup disabled."; }
        }
        catch (Exception ex)
        {
            Status.Text = "Could not change startup setting: " + ex.Message;
            initialized = false;
            StartWithWindows.IsChecked = StartWithWindows.IsChecked != true;
            initialized = true;
        }
    }
    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
