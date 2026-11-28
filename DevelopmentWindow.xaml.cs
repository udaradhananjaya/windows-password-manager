using System.Windows;
using System.Windows.Interop;

namespace HelloAutofill;

public class DevelopmentOption
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string Key { get; set; } = "";
    public static List<DevelopmentOption> Defaults() => Enumerable.Range(1, 4).Select(i => new DevelopmentOption { Name = $"Placeholder {i}" }).ToList();
}

public partial class DevelopmentWindow : Window
{
    private readonly HashSet<string> loginKeys;
    public List<DevelopmentOption> Options { get; }
    public DevelopmentWindow(IEnumerable<DevelopmentOption> options, IEnumerable<string> existingKeys)
    {
        InitializeComponent();
        Options = options.Select(x => new DevelopmentOption { Name = x.Name, Enabled = x.Enabled, Key = x.Key }).ToList();
        loginKeys = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        OptionsList.ItemsSource = Options;
        SourceInitialized += (_, _) => { int dark = 1; Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); };
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in Options)
        {
            option.Key = (option.Key ?? "").Trim().ToUpperInvariant();
            if (option.Key.Length > 0 && (option.Key.Length != 1 || !(option.Key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')))
            { Message.Text = "Use a letter A-Z or number 0-9 for each shortcut."; return; }
            if (!option.Enabled) continue;
            if (option.Key.Length == 0) { Message.Text = "Assign a shortcut to each enabled option."; return; }
            if (loginKeys.Contains(option.Key) || !used.Add(option.Key))
            { Message.Text = "Each enabled shortcut must differ from other options and login shortcuts."; return; }
        }
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
