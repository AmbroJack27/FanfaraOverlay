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
        // Transparent, click-through, always-on-top overlay that renders the notifications.
        _overlay = new OverlayWindow(_config);
        _overlay.Show();

        // Watch Steam for the running game and freshly unlocked achievements.
        _steam = new SteamWatcher(_config.PollMs);
        _steam.AchievementUnlocked += (name, percent) =>
            _overlay?.Dispatcher.Invoke(() => _overlay.ShowAchievement(name, percent, _config));
        _steam.Start();

        SetupTray();
        ShowSettings();   // open the settings window on launch so you can see/change things
    }

    private void SetupTray()
    {
        System.Drawing.Icon icon;
        try
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            icon = exe != null ? System.Drawing.Icon.ExtractAssociatedIcon(exe)! : System.Drawing.SystemIcons.Application;
        }
        catch { icon = System.Drawing.SystemIcons.Application; }

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
