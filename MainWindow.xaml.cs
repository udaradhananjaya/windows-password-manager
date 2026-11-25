using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Security.Credentials.UI;

namespace HelloAutofill;

public partial class MainWindow : Window
{
    private readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HelloAutofill", "entries.json");
    private List<Entry> entries = new();
    private readonly Dictionary<int, Entry> registered = new();
    private IntPtr handle;
    private bool busy;
    private Guid? editing;
    private System.Windows.Forms.NotifyIcon? trayIcon;
    private readonly IntPtr initialForeground = Native.GetForegroundWindow();
    private bool exiting;
    private readonly System.Windows.Threading.DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromHours(12) };
    private string? notifiedUpdate;
    public MainWindow() { InitializeComponent(); SourceInitialized += OnReady; Closed += OnClosed; StateChanged += OnStateChanged; System.Windows.Application.Current.SessionEnding += (_, _) => exiting = true; }
    private void OnReady(object? sender, EventArgs e)
    {
        handle = new WindowInteropHelper(this).Handle;
        PlaceOnActiveMonitor();
        int darkMode = 1; Native.DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int));
        HwndSource.FromHwnd(handle).AddHook(Hook);
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/res/win_hello.ico")) ?? throw new InvalidOperationException("App icon missing.");
        using (resource.Stream) { using var icon = new System.Drawing.Icon(resource.Stream); trayIcon = new System.Windows.Forms.NotifyIcon { Icon = (System.Drawing.Icon)icon.Clone(), Text = "Hello Autofill", Visible = true }; }
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => RestoreWindow());
        menu.Items.Add("Exit", null, (_, _) => { exiting = true; Close(); });
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => RestoreWindow();
        RefreshWindows();
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        updateTimer.Start();
        _ = CheckForUpdatesAsync();
        try { if (File.Exists(configPath)) entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(configPath)) ?? new(); Refresh(); RegisterEntries(); }
        catch (Exception ex) { Status.Text = "Could not load entries: " + ex.Message; }
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; HideToTray(); }
        base.OnClosing(e);
    }
    private void HideToTray() { Hide(); ShowInTaskbar = false; }
    private void PlaceOnActiveMonitor()
    {
        var screen = initialForeground == IntPtr.Zero ? System.Windows.Forms.Screen.PrimaryScreen : System.Windows.Forms.Screen.FromHandle(initialForeground);
        if (screen == null) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        int width = 486;
        int height = 869;
        var area = screen.WorkingArea;
        int x = Math.Max(area.Left, area.Right - width - (int)Math.Round(16 * dpi.DpiScaleX));
        int y = Math.Max(area.Top, area.Top + (area.Height - height) / 2);
        Native.SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x0004 | 0x0010);
    }
    private void About_Click(object sender, RoutedEventArgs e) { var about = new AboutWindow { Owner = this }; about.ShowDialog(); }
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await UpdateChecker.CheckAsync();
            if (!exiting && update.IsNewer && notifiedUpdate != update.Tag)
            {
                notifiedUpdate = update.Tag;
                Status.Text = $"Update {update.Tag} available. Open About to visit releases.";
                trayIcon?.ShowBalloonTip(5000, "Hello Autofill update", $"Version {update.Tag} is available. Open About to visit releases.", System.Windows.Forms.ToolTipIcon.Info);
            }
        }
        catch (Exception ex) when (UpdateChecker.IsCheckFailure(ex)) { }
    }
    private void Options_Click(object sender, RoutedEventArgs e) { var options = new OptionsWindow { Owner = this }; options.ShowDialog(); }
    private void OnClosed(object? sender, EventArgs e) { updateTimer.Stop(); Unregister(); if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Icon?.Dispose(); trayIcon.ContextMenuStrip?.Dispose(); trayIcon.Dispose(); } }
    private void OnStateChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Minimized) HideToTray(); }
    private void RestoreWindow() { ShowInTaskbar = true; Show(); WindowState = WindowState.Normal; Activate(); }
    private void RefreshWindows_Click(object sender, RoutedEventArgs e) => RefreshWindows();
    private void RefreshWindows() { WindowPicker.ItemsSource = Native.OpenWindows(handle); WindowPicker.SelectedIndex = -1; }
    private void WindowPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowPicker.SelectedItem is WindowChoice choice) { Executable.Text = choice.Executable; WindowTitle.Text = choice.Title; }
    }
    private void Unregister() { foreach (int id in registered.Keys) Native.UnregisterHotKey(handle, id); registered.Clear(); }
    private static bool IsShortcutKey(char key) => key is >= 'A' and <= 'Z' or >= '0' and <= '9';
    private void RegisterEntries()
    {
        Unregister(); var failed = new List<string>(); int id = 1;
        foreach (var entry in entries) { if (entry.Key.Length != 1 || !IsShortcutKey(entry.Key[0]) || !Native.RegisterHotKey(handle, id, 0x0001 | 0x0002 | 0x0004 | 0x4000, entry.Key[0])) failed.Add(entry.Name); else registered[id] = entry; id++; }
        Status.Text = failed.Count == 0 ? "Ready. Select New to add a login." : "Shortcut unavailable: " + string.Join(", ", failed);
    }
    private void Refresh() { EntriesList.ItemsSource = null; EntriesList.ItemsSource = entries; }
    private void Persist() { Directory.CreateDirectory(Path.GetDirectoryName(configPath)!); string tmp = configPath + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true })); File.Move(tmp, configPath, true); }
    private void SelectEntry(object sender, SelectionChangedEventArgs e)
    {
        if (EntriesList.SelectedItem is not Entry entry) return;
        editing = entry.Id; EntryName.Text = entry.Name; Executable.Text = entry.Executable; WindowTitle.Text = entry.Title; Shortcut.Text = entry.Key;
        Username.Clear(); Password.Clear(); Status.Text = "Enter the username and password to update this entry.";
    }
    private void New_Click(object sender, RoutedEventArgs e) { EntriesList.SelectedItem = null; editing = null; EntryName.Clear(); Executable.Clear(); WindowTitle.Clear(); Shortcut.Clear(); Username.Clear(); Password.Clear(); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string name = EntryName.Text.Trim(), exe = Executable.Text.Trim(), title = WindowTitle.Text.Trim(), key = Shortcut.Text.Trim().ToUpperInvariant();
            if (name.Length == 0 || exe.Length == 0 || title.Length == 0 || key.Length != 1 || !IsShortcutKey(key[0]) || Username.Text.Length == 0 || Password.Password.Length == 0) throw new InvalidOperationException("Complete every field and choose a shortcut letter A–Z or digit 0–9.");
            if (new[] { "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe" }.Contains(exe, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Browser entries are not supported yet; website addresses cannot be verified.");
            if (exe.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Enter only an executable name ending in .exe.");
            if (Username.Text.Any(char.IsControl) || Password.Password.Any(char.IsControl)) throw new InvalidOperationException("Control characters in credentials are not supported.");
            if (entries.Any(x => x.Id != editing && x.Key == key)) throw new InvalidOperationException("That shortcut is already assigned.");
            var entry = new Entry(editing ?? Guid.NewGuid(), name, exe, title, key);
            CredentialStore.Save(entry.Target, new Login(Username.Text, Password.Password));
            int index = entries.FindIndex(x => x.Id == entry.Id); if (index < 0) entries.Add(entry); else entries[index] = entry;
            Persist(); Password.Clear(); Refresh(); RegisterEntries();
        }
        catch (Exception ex) { Status.Text = "Could not save: " + ex.Message; }
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (editing is null) { Status.Text = "Select an entry first."; return; }
        if (System.Windows.MessageBox.Show("Delete this saved login?", "Hello Autofill", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { var entry = entries.Single(x => x.Id == editing); CredentialStore.Delete(entry.Target); entries.Remove(entry); Persist(); New_Click(sender, e); Refresh(); RegisterEntries(); }
        catch (Exception ex) { Status.Text = "Could not delete: " + ex.Message; }
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && !busy && registered.TryGetValue(wParam.ToInt32(), out var entry)) { handled = true; _ = InsertAsync(entry); }
        return IntPtr.Zero;
    }
    private async Task InsertAsync(Entry entry)
    {
        busy = true;
        try
        {
            IntPtr target = Native.GetForegroundWindow();
            if (target == IntPtr.Zero || target == handle || !Native.Matches(target, entry)) throw new InvalidOperationException("The active application or window title does not match this entry.");
            IntPtr initial = Native.FocusedControl(target);
            if (initial == IntPtr.Zero) throw new InvalidOperationException("Could not identify the focused field.");
            var available = await UserConsentVerifier.CheckAvailabilityAsync();
            if (available != UserConsentVerifierAvailability.Available) throw new InvalidOperationException($"Windows Hello unavailable: {available}.");
            var result = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(target, "Verify to fill " + entry.Name);
            if (result != UserConsentVerificationResult.Verified) throw new InvalidOperationException($"Verification not completed: {result}.");
            if (Native.GetForegroundWindow() != target || Native.FocusedControl(target) != initial || !Native.Matches(target, entry)) throw new InvalidOperationException("Target or focus changed during verification.");
            var login = CredentialStore.Read(entry.Target) ?? throw new InvalidOperationException("Saved credential not found.");
            Native.TypeUnicode(login.Username, target, initial);
            if (!Native.Matches(target, entry)) throw new InvalidOperationException("Target changed; typing stopped.");
            Native.SendTab();
            await Task.Delay(200);
            if (Native.GetForegroundWindow() != target || !Native.Matches(target, entry)) throw new InvalidOperationException("Target changed after Tab; password not typed.");
            var next = Native.FocusedControl(target);
            if (next == IntPtr.Zero) throw new InvalidOperationException("Could not identify the field after Tab; password not typed.");
            Native.TypeUnicode(login.Password, target, next);
            Status.Text = "Filled " + entry.Name + ".";
        }
        catch (Exception ex) { Status.Text = "Insertion stopped: " + ex.Message; }
        finally { busy = false; }
    }
}
record Entry(Guid Id, string Name, string Executable, string Title, string Key)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Target => "HelloAutofill/Entry/" + Id.ToString("N");
    [System.Text.Json.Serialization.JsonIgnore] public string Display => $"{Name} — Ctrl+Alt+Shift+{Key}";
}
record WindowChoice(string Executable, string Title)
{
    public string Display => $"{Title} - {Executable}";
}
record Login(string Username, string Password);

static class CredentialStore
{
    public static void Save(string target, Login login)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(login));
        if (bytes.Length > 2560) throw new InvalidOperationException("Login is too long for Credential Manager.");
        IntPtr blob = Marshal.AllocCoTaskMem(bytes.Length);
        try { Marshal.Copy(bytes, 0, blob, bytes.Length); var c = new Native.CREDENTIAL { Type = 1, TargetName = target, CredentialBlobSize = (uint)bytes.Length, CredentialBlob = blob, Persist = 2, UserName = login.Username }; if (!Native.CredWrite(ref c, 0)) throw new Win32Exception(); }
        finally { Array.Clear(bytes); for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0); Marshal.FreeCoTaskMem(blob); }
    }
    public static Login? Read(string target)
    {
        if (!Native.CredRead(target, 1, 0, out var ptr)) { int err = Marshal.GetLastWin32Error(); if (err == 1168) return null; throw new Win32Exception(err); }
        try { var c = Marshal.PtrToStructure<Native.CREDENTIAL>(ptr); byte[] bytes = new byte[c.CredentialBlobSize]; try { Marshal.Copy(c.CredentialBlob, bytes, 0, bytes.Length); return JsonSerializer.Deserialize<Login>(bytes); } finally { Array.Clear(bytes); } }
        finally { Native.CredFree(ptr); }
    }
    public static void Delete(string target) { if (!Native.CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(); }
}

static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct CREDENTIAL { public uint Flags, Type; public string? TargetName, Comment; public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten; public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist, AttributeCount; public IntPtr Attributes; public string? TargetAlias, UserName; }
    [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO { public uint cbSize, flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION data; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra; }
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredWrite(ref CREDENTIAL credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] public static extern void CredFree(IntPtr buffer);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
    public static List<WindowChoice> OpenWindows(IntPtr ownWindow)
    {
        var windows = new List<WindowChoice>();
        EnumWindows((window, _) =>
        {
            if (window == ownWindow || !IsWindowVisible(window)) return true;
            int length = GetWindowTextLength(window); if (length <= 0) return true;
            var title = new StringBuilder(length + 1); if (GetWindowText(window, title, title.Capacity) == 0) return true;
            try { GetWindowThreadProcessId(window, out uint pid); using var process = Process.GetProcessById((int)pid); string exe = Path.GetFileName(process.MainModule!.FileName); windows.Add(new WindowChoice(exe, title.ToString())); } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException) { }
            return true;
        }, IntPtr.Zero);
        return windows.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    public static bool Matches(IntPtr window, Entry entry)
    {
        try { GetWindowThreadProcessId(window, out uint pid); using var process = Process.GetProcessById((int)pid); string exe = Path.GetFileName(process.MainModule!.FileName); var title = new StringBuilder(1024); GetWindowText(window, title, title.Capacity); return string.Equals(exe, entry.Executable, StringComparison.OrdinalIgnoreCase) && string.Equals(title.ToString(), entry.Title, StringComparison.Ordinal); }
        catch { return false; }
    }
    public static void SendTab()
    {
        var inputs = new INPUT[2]; inputs[0].type = inputs[1].type = 1; inputs[0].data.keyboard.vk = inputs[1].data.keyboard.vk = 9; inputs[1].data.keyboard.flags = 2;
        if (SendInput(2, inputs, Marshal.SizeOf<INPUT>()) != 2) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected Tab input.");
    }
    public static IntPtr FocusedControl(IntPtr window) { uint thread = GetWindowThreadProcessId(window, out _); var info = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() }; return thread != 0 && GetGUIThreadInfo(thread, ref info) ? info.hwndFocus : IntPtr.Zero; }
    public static void TypeUnicode(string value, IntPtr target, IntPtr focus)
    {
        foreach (char ch in value) { if (GetForegroundWindow() != target || FocusedControl(target) != focus) throw new InvalidOperationException("Focus changed; typing stopped."); var inputs = new INPUT[2]; inputs[0].type = inputs[1].type = 1; inputs[0].data.keyboard.scan = inputs[1].data.keyboard.scan = ch; inputs[0].data.keyboard.flags = 4; inputs[1].data.keyboard.flags = 4 | 2; if (SendInput(2, inputs, Marshal.SizeOf<INPUT>()) != 2) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected simulated input. Check application privilege levels."); }
    }
}

