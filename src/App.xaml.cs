using System.Windows;

namespace Fanfara;

public partial class App : System.Windows.Application
{
    private OverlayWindow? _overlay;
    private SteamWatcher? _steam;
    private SettingsWindow? _settings;
    private System.Windows.Forms.NotifyIcon? _tray;
    private readonly Config _config = Config.Load();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Did Windows launch us automatically at boot? (see StartupManager)
        bool autoStarted = Array.Exists(e.Args, a =>
            string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        // Keep the "run at startup" registry entry in sync with the saved preference
        // (also refreshes the stored path if the app was moved or reinstalled).
        StartupManager.Apply(_config.StartWithWindows);

        // Transparent, click-through, always-on-top overlay that renders the notifications.
        _overlay = new OverlayWindow(_config);
        _overlay.Show();

        // Watch Steam for the running game and freshly unlocked achievements.
        _steam = new SteamWatcher(_config.PollMs);
        _steam.AchievementUnlocked += (name, percent) =>
            _overlay?.Dispatcher.Invoke(() => _overlay.ShowAchievement(name, percent, _config));
        _steam.Start();

        SetupTray();

        // When launched by Windows at boot, stay quietly in the tray.
        // When launched by the user, open settings so they can see/change things.
        if (!autoStarted)
            ShowSettings();
    }

    private void SetupTray()
    {
        // Load our multi-size icon crisply. Prefer the packaged resource at 32x32;
        // fall back to the exe's own icon, then to a system default.
        System.Drawing.Icon icon = System.Drawing.SystemIcons.Application;
        try
        {
            var res = System.Windows.Application.GetResourceStream(new Uri("icon.ico", UriKind.Relative));
            if (res != null)
                icon = new System.Drawing.Icon(res.Stream, new System.Drawing.Size(32, 32));
            else
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exe != null) icon = System.Drawing.Icon.ExtractAssociatedIcon(exe)!;
            }
        }
        catch
        {
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exe != null) icon = System.Drawing.Icon.ExtractAssociatedIcon(exe)!;
            }
            catch { /* keep system default */ }
        }

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Fanfara",
            Visible = true,
            Icon = icon
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Impostazioni", null, (_, _) => ShowSettings());
        menu.Items.Add("Prova notifica", null, (_, _) =>
            _overlay?.ShowPreview(_config.Style, _config.Sound, _config.Position, 0.6, "Anteprima!"));
        menu.Items.Add("Esci", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowSettings();
    }

    private void ShowSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow(_config, _overlay!);
            _settings.Closed += (_, _) => _settings = null;
            _settings.Show();
        }
        _settings.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _steam?.Stop();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}
