# Hello Autofill

A Windows 11 desktop prototype for multiple named logins. Each entry has a Ctrl+Alt+Shift+letter or digit shortcut, an executable name, and an exact, case-sensitive window title. Login secrets are stored separately in Windows Credential Manager; entry names and matching settings are stored in `%LOCALAPPDATA%\HelloAutofill\entries.json`.

1. Open the app and select **New**. Enter a descriptive name, the executable filename (for example `javaw.exe`), exact window title, shortcut letter or digit, username, and password. Select **Save**. A shortcut reported as unavailable will not work until the conflicting program is closed and this app is restarted.
2. Focus the username field in the specified desktop app. Press its shortcut and complete Windows Hello. The app types the username, presses Tab, types the password, then presses Enter to submit the form.
3. Test first with disposable credentials and verify that Tab moves to the correct password field. A login form may have a different tab order, and this prototype cannot reliably identify the password field in every app. If Tab moves to another field, the password can be typed there. Use this feature only after verifying the form's tab order.

Edit an entry by selecting it, changing its settings, and re-entering both username and password; save. Delete removes the credential and entry. Keep the app open to use shortcuts. Window titles must match exactly and may change. Executable names and titles can be spoofed, so these checks are a guard against accidents, not a strong application identity check. Common browsers are blocked: website address matching is not implemented. Do not configure other browsers as desktop targets.

The prior single-login credential, `HelloAutofill/Default`, is left untouched in Windows Credential Manager and is not migrated or displayed; create new entries manually. You can remove the old entry using Windows Credential Manager after confirming the new one works.

Build using .NET 8 SDK and `dotnet build`. This machine has an isolated SDK at `%LOCALAPPDATA%\HelloAutofillDotnet\dotnet.exe`. Windows 11 build 22000 or later and configured Windows Hello are required. The initial restore may need `dotnet restore --source https://api.nuget.org/v3/index.json`.

Windows Hello gates filling through this app; it does not cryptographically bind access to saved secrets. Other processes running as the same Windows user may be able to read them, and malware can observe simulated input. Focus checks cannot eliminate all races. This is not a security-reviewed password manager.

## Window picker and tray

Select **Refresh windows**, then choose an open window to populate its executable and exact title. Some protected or elevated windows may not appear. Check the values before saving; window titles can change. Minimize to keep the app and its shortcuts running in the notification area. Double-click the tray icon or choose **Open** from its menu to restore the window; choose **Exit** to stop it. Closing the main window also hides it to the tray. Use the tray menu’s **Exit** command to quit.

## Options

Use **Options** to enable or disable startup when you sign in to Windows. Startup launches the app in the system tray; use the tray icon to open it. The setting applies only to your Windows account and points to the currently running executable; update the setting if you move or replace that executable. If startup was enabled in an older version, disable and re-enable it to update the command.

## About and updates

**About** appears on first launch for each Windows user and shows the app version, security limitations, a manual update check, and a link to GitHub releases. The app checks the public GitHub latest-release API on startup and every 12 hours while running, and shows a tray notification when it finds a newer version. Checks send an unauthenticated request to GitHub. A failed check does not mean the app is up to date. The user downloads and replaces the executable manually; no automatic code execution or binary replacement is performed.

To publish a future release, set the project version to a three-part version (for example, `0.1.2`), commit and push it, then push the matching tag (for example, `v0.1.2`). GitHub Actions builds a self-contained Windows x64 executable and SHA-256 checksum and creates a draft release. Review and publish the draft on GitHub to make it available to the updater. The executable is not code-signed; the checksum checks integrity but does not establish publisher identity. Close the running app before replacing its executable, and update the Windows startup setting if the executable path changes.
