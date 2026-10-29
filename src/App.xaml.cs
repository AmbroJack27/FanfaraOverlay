using System.Linq;
using System.Text.Json;
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

    private AchievementStore? _store;        // local history/collection, shared by live unlocks + backfill
    private GamesStore? _games;              // per-game 100% completion (cards + leaderboard score)
    private int _backfillRunning;            // 0/1 guard so only one sync runs at a time

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
        // Single instance: the named mutex is also what the Inno Setup installer watches (AppMutex).
        // If another Fanfara is already running we own nothing new — quit immediately so we never
        // end up with several overlays/Steam workers fighting each other.
        bool createdNew;
        try { _appMutex = new Mutex(true, "FanfaraOverlayAppMutex", out createdNew); }
        catch { createdNew = true; }   // if we can't check, don't block the only instance
        if (!createdNew)
        {
            Shutdown();
            return;
        }

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

        // Local store of unlocked achievements (feeds the History + Collection windows).
        _store = new AchievementStore();
        // Per-game 100% completion (feeds the Classifica "100% games" cards and the leaderboard score).
        _games = new GamesStore();

        // Transparent, click-through, always-on-top overlay that renders the notifications.
        _overlay = new OverlayWindow(_config);
        _overlay.Show();

        // Watch Steam for the running game and freshly unlocked achievements.
        _steam = new SteamWatcher(_config);
        _steam.AchievementUnlocked += (name, percent, desc, appid, api) =>
        {
            _overlay?.Dispatcher.Invoke(() => _overlay.ShowAchievement(name, percent, desc, _config));
            // Record the live unlock so it appears in History/Collection even without a full sync.
            try
            {
                _store?.Upsert(new AchievementRecord
                {
                    Appid = appid,
                    Api = string.IsNullOrEmpty(api) ? "live:" + name : api,
                    Name = name,
                    Desc = desc,
                    Unlock = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Percent = percent
                });
                _store?.Save();
                // Nudge the per-game count too (total stays from the last sync, which flips 100% on).
                _games?.BumpUnlocked(appid, "", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                _games?.Save();
                RefreshData();
            }
            catch { }
        };
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

        // First launch imports everything (with a welcome); afterwards a quiet background refresh at
        // startup (when it's been a while) keeps History/Collection current — new unlocks, achievements
        // earned while Fanfara was closed or on another PC, and real icons for live-captured unlocks.
        if (_config.SyncHistory)
        {
            bool firstRun = !_config.HistoryImported || (_store?.Count ?? 0) == 0;
            if (firstRun || SyncIsStale())
                _ = RunBackfillAsync(manual: false, firstRun: firstRun);
        }
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
        menu.Items.Add(Loc.S(lang, "openApp"), null, (_, _) => ShowSettings());
        menu.Items.Add(Loc.S(lang, "history"), null, (_, _) => ShowSettings("history.html"));
        menu.Items.Add(Loc.S(lang, "collection"), null, (_, _) => ShowSettings("collection.html"));
        menu.Items.Add(Loc.S(lang, "leaderboard"), null, (_, _) => ShowSettings("classifica.html"));
        menu.Items.Add(Loc.S(lang, "syncNow"), null, (_, _) => _ = RunBackfillAsync(manual: true));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Loc.S(lang, "testNotif"), null, (_, _) =>
            _overlay?.ShowPreview(_config.Style, _config.Sound, _config.Position, 0.6,
                Loc.S(_config.EffectiveLanguage, "preview"), _config.Volume, Loc.S(_config.EffectiveLanguage, "previewDesc")));
        menu.Items.Add(Loc.S(lang, "checkUpdates"), null, (_, _) => _ = CheckForUpdatesAsync(silent: false));
        menu.Items.Add(Loc.S(lang, "quit"), null, (_, _) => QuitApp());
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

    /// <summary>Show the single app window, optionally jumping straight to a screen (history/collection).</summary>
    private void ShowSettings(string? page = null)
    {
        if (_settings == null)
        {
            // First creation of the app window — the logo intro plays here, once.
            _settings = new SettingsWindow(_config, _overlay!, _store!, _games!, page);
            _settings.Show();
        }
        else
        {
            // Window already exists (just hidden to tray): show it again without reloading,
            // so the intro does not replay. Jump to a screen only if one was requested.
            if (page != null) _settings.NavigateTo(page);
            if (!_settings.IsVisible) _settings.Show();
        }
        if (_settings.WindowState == System.Windows.WindowState.Minimized)
            _settings.WindowState = System.Windows.WindowState.Normal;
        _settings.Activate();
    }

    /// <summary>Re-send data to the window if it is currently showing the History/Collection screen.</summary>
    private void RefreshData()
    {
        try { Dispatcher.Invoke(() => _settings?.RefreshData()); }
        catch { }
    }

    /// <summary>True if it's been long enough since the last successful sync to refresh again.</summary>
    private bool SyncIsStale()
    {
        if (string.IsNullOrEmpty(_config.LastSyncUtc)) return true;
        if (DateTime.TryParse(_config.LastSyncUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last))
            return (DateTime.UtcNow - last) > TimeSpan.FromHours(12);
        return true;
    }

    /// <summary>
    /// Imports/refreshes the achievement history from Steam through the proxy.
    /// Three modes: manual (tray button → message box), firstRun (welcome + result balloon),
    /// periodic (silent; only announces newly found achievements).
    /// </summary>
    private async Task RunBackfillAsync(bool manual, bool firstRun = false)
    {
        if (_store == null) return;
        if (Interlocked.Exchange(ref _backfillRunning, 1) == 1)
        {
            if (manual) Balloon(Loc.S(_config.EffectiveLanguage, "syncBusy"));
            return;
        }
        var lang = _config.EffectiveLanguage;

        // Start feedback: a note for a manual sync, a welcome on the first import, nothing for a quiet refresh.
        if (manual) Balloon(Loc.S(lang, "syncStart"));
        else if (firstRun) Balloon(Loc.S(lang, "syncWelcome"));

        try
        {
            var hist = new SteamHistory(_config, _store, _games!);
            var res = await hist.BackfillAsync();
            switch (res)
            {
                case SteamHistory.Result.Ok:
                    RefreshData();
                    // Register the refreshed 100% score so friends (and, if opted in, the world board) see it.
                    if (_games != null) { var lb = new Leaderboard(_config); if (lb.Ready) _ = lb.SubmitAsync(_games.PerfectCount, _games.TotalUnlocked); }
                    if (manual) System.Windows.MessageBox.Show(string.Format(Loc.S(lang, "syncDone"), _store.Count), "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (firstRun) Balloon(string.Format(Loc.S(lang, "syncDone"), _store.Count));
                    else if (hist.LastAdded > 0) Balloon(string.Format(Loc.S(lang, "syncNew"), hist.LastAdded));   // quiet refresh: only if there's something new
                    break;
                case SteamHistory.Result.ProfilePrivate:
                    if (manual) System.Windows.MessageBox.Show(Loc.S(lang, "syncPrivate"), "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (firstRun) Balloon(Loc.S(lang, "syncPrivate"));
                    break;
                case SteamHistory.Result.NoSteamId:
                    if (manual) System.Windows.MessageBox.Show(Loc.S(lang, "syncNoId"), "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case SteamHistory.Result.NoProxy:
                    if (manual) System.Windows.MessageBox.Show(Loc.S(lang, "syncNoProxy"), "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                default:
                    if (manual) System.Windows.MessageBox.Show(Loc.S(lang, "syncError"), "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }
        catch { if (manual) System.Windows.MessageBox.Show(Loc.S(lang, "syncError"), "Fanfara", MessageBoxButton.OK, MessageBoxImage.Information); }
        finally { Interlocked.Exchange(ref _backfillRunning, 0); }
    }

    private void Balloon(string text)
    {
        try { Dispatcher.Invoke(() => _tray?.ShowBalloonTip(8000, "Fanfara", text, System.Windows.Forms.ToolTipIcon.Info)); }
        catch { }
    }

    /// <summary>Really quit (from the tray "Quit"): allow the window to close, then shut down.</summary>
    private void QuitApp()
    {
        if (_settings != null) _settings.AllowRealClose = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _steam?.Stop();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}
