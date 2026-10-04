using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Fanfara;

public partial class App : System.Windows.Application
{
    private OverlayWindow? _overlay;
    private SteamWatcher? _steam;
    private SettingsWindow? _settings;
    private System.Windows.Forms.NotifyIcon? _tray;
    private static Mutex? _appMutex;   // lets the installer detect/close a running instance
    private readonly Config _config = Config.Load();

    public App()
    {
        // Steam worker mode: run the isolated Steam process and exit, never building any UI.
        var cl = Environment.GetCommandLineArgs();
        int i = Array.FindIndex(cl, a => string.Equals(a, "--steam-worker", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 < cl.Length && uint.TryParse(cl[i + 1], out var app))
            Environment.Exit(SteamWorker.Run(app));
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Named mutex the Inno Setup installer watches (AppMutex) to update while running.
        try { _appMutex = new Mutex(true, "FanfaraOverlayAppMutex"); } catch { }

        // Did Windows launch us automatically at boot? (see StartupManager)
        bool autoStarted = Array.Exists(e.Args, a =>
            string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        // Keep the "run at startup" registry entry in sync with the saved preference
        // (also refreshes the stored path if the app was moved or reinstalled).
        StartupManager.Apply(_config.StartWithWindows);

        // Pre-prepare installed Steam games so notifications show over them from the first
        // launch (disable fullscreen optimizations). Runs in the background, once per game.
        if (_config.GameOverlayFix)
            Task.Run(() => { try { SteamLibrary.PreoptimizeAll(_config); } catch { } });

        // Transparent, click-through, always-on-top overlay that renders the notifications.
        _overlay = new OverlayWindow(_config);
        _overlay.Show();

        // Watch Steam for the running game and freshly unlocked achievements.
        _steam = new SteamWatcher(_config);
        _steam.AchievementUnlocked += (name, percent) =>
            _overlay?.Dispatcher.Invoke(() => _overlay.ShowAchievement(name, percent, _config));
        _steam.GameOptimized += () => Dispatcher.Invoke(ShowRestartGameTip);
        _steam.Start();

        SetupTray();

        // When launched by Windows at boot, stay quietly in the tray.
        // When launched by the user, open settings so they can see/change things.
        if (!autoStarted)
            ShowSettings();

        // Quiet check for a newer version on GitHub (if enabled).
        if (_config.CheckUpdates)
            _ = CheckForUpdatesAsync(silent: true);
    }

    private async Task CheckForUpdatesAsync(bool silent)
    {
        var lang = _config.EffectiveLanguage;
        var info = await Updater.CheckAsync();
        if (info == null)
        {
            if (!silent)
                System.Windows.MessageBox.Show(Loc.S(lang, "noUpdateBody"),
                    "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var r = System.Windows.MessageBox.Show(
            string.Format(Loc.S(lang, "updateAvailBody"), info.Version),
            Loc.S(lang, "updateAvailTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        if (await Updater.DownloadAndRunAsync(info))
            Shutdown();   // free the files so the installer can replace them
        else
            System.Windows.MessageBox.Show(
                Loc.S(lang, "downloadFailBody"),
                "Fanfara", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        var lang = _config.EffectiveLanguage;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.S(lang, "settings"), null, (_, _) => ShowSettings());
        menu.Items.Add(Loc.S(lang, "testNotif"), null, (_, _) =>
            _overlay?.ShowPreview(_config.Style, _config.Sound, _config.Position, 0.6, Loc.S(_config.EffectiveLanguage, "preview")));
        menu.Items.Add(Loc.S(lang, "checkUpdates"), null, (_, _) => _ = CheckForUpdatesAsync(silent: false));
        menu.Items.Add(Loc.S(lang, "quit"), null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowSettings();
    }

    private void ShowRestartGameTip()
    {
        try
        {
            _tray?.ShowBalloonTip(9000, "Fanfara",
                Loc.S(_config.EffectiveLanguage, "restartGame"),
                System.Windows.Forms.ToolTipIcon.Info);
        }
        catch { /* notifications disabled — ignore */ }
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
