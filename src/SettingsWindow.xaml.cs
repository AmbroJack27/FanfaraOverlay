using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace Fanfara;

public partial class SettingsWindow : Window
{
    private readonly Config _config;
    private readonly OverlayWindow _overlay;
    private readonly AchievementStore _store;
    private readonly GamesStore _games;
    private readonly Leaderboard _lb;
    private string? _pendingPage;   // page to open first (home by default)
    private bool _coreReady;
    private bool _syncRunning;      // a Steam sync is in progress (shown on the Home)

    /// <summary>The Home asked for a Steam sync (the app owns the sync, so it handles this).</summary>
    public event Action? SyncRequested;
    private string? _artScriptId;
    /// <summary>
    /// 0.6.1: give every page the addresses of the games whose pictures moved (window.FAN_ART), from the
    /// next page on and — when refresh is true — on the page that is showing now too.
    /// </summary>
    public async Task ApplyArtAsync(bool refresh = true)
    {
        try
        {
            var core = Web.CoreWebView2; if (core == null) return;
            var js = "window.FAN_ART=" + GameArt.ScriptJson() + ";";
            if (_artScriptId != null) core.RemoveScriptToExecuteOnDocumentCreated(_artScriptId);
            _artScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(js);
            if (refresh) { await core.ExecuteScriptAsync(js); RefreshData(); }
        }
        catch { }
    }

    /// <summary>«Update now» pressed in the Home (0.6.0).</summary>
    public event Action? UpdateRequested;
    private string? _pendingNews;   // «What's new» waiting for the Home to be shown
    private string? _lastNews;      // the last «What's new», so the bell can open it again

    /// <summary>After an update: show the «What's new» window in the Home (now, or as soon as the Home opens).</summary>
    public void ShowNews(string json)
    {
        _pendingNews = json; _lastNews = json;
        if (OnHome) PushNews();
    }
    private void PushNews()
    {
        if (_pendingNews == null) return;
        try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraNews && window.fanfaraNews({_pendingNews})"); _pendingNews = null; } catch { }
    }
    /// <summary>One-click update progress for the Home toast: state = download | verify | install | wait | fail.</summary>
    public void PushUpdate(string state, int pct = 0)
    {
        try { if (OnHome) Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraUpdate && window.fanfaraUpdate({JsonSerializer.Serialize(new { state, pct })})"); } catch { }
    }

    public SettingsWindow(Config config, OverlayWindow overlay, AchievementStore store, GamesStore games, string? initialPage = null)
    {
        _config = config;
        _overlay = overlay;
        _store = store;
        _games = games;
        _lb = new Leaderboard(config);
        _pendingPage = initialPage;
        InitializeComponent();
        FitToScreen();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Open big enough to show the whole Home on one screen: about 90% of the screen's free area
    /// (taskbar excluded), never above 1720×1135 and never below the window's minimum size.
    /// SystemParameters.WorkArea is in the same device-independent units as Width/Height, so this
    /// also works with Windows display scaling (125%, 150%...). The window stays centred.
    /// </summary>
    private void FitToScreen()
    {
        try
        {
            var area = SystemParameters.WorkArea;
            Width  = Math.Max(MinWidth,  Math.Min(1720, Math.Round(area.Width  * 0.90)));
            Height = Math.Max(MinHeight, Math.Min(1135, Math.Round(area.Height * 0.92)));
        }
        catch { /* keep the XAML size */ }
    }

    // ---- dark title bar matching the app theme ----
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTitleBar(_config.Theme != "light");
    }

    /// <summary>Set true right before a real app shutdown so the window can actually close.</summary>
    public bool AllowRealClose;

    // Closing the window (the X) hides it to the tray instead of destroying it, so reopening from
    // the tray shows the same window — no reload, and the logo intro does NOT play again. The intro
    // runs only on a fresh app launch, when the window is created for the first time.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowRealClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void ApplyTitleBar(bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            // COLORREF is 0x00BBGGRR. Dark: #0b0f18 / #E8EBF3 — Light: #eaeef6 / #1b2233
            int caption = dark ? 0x00180F0B : 0x00F6EEEA;
            int text    = dark ? 0x00F3EBE8 : 0x00332218;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        catch { /* older Windows: ignore */ }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Must match the overlay window's environment options exactly: WebView2 allows only one
        // set of browser arguments per user-data folder per process, so a mismatch would leave
        // this window blank.
        var opts = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
        };
        var env = await CoreWebView2Environment.CreateAsync(null, null, opts);
        await Web.EnsureCoreWebView2Async(env);
        WebLock.Apply(Web);   // no zoom, no F12, no browser shortcuts

        Web.CoreWebView2.WebMessageReceived += OnWebMessage;
        Web.CoreWebView2.NavigationCompleted += (_, _) => OnNavigated();
        await ApplyArtAsync(refresh: false);   // 0.6.1: every page knows where the pictures of newer games are

        _coreReady = true;
        Web.CoreWebView2.Navigate(PageUrl(_pendingPage ?? "home.html"));
    }

    /// <summary>Full file:// URL for a web page, carrying the current theme + language so it opens correctly.</summary>
    private string PageUrl(string page)
    {
        var html = Path.Combine(AppContext.BaseDirectory, "web", page);
        var uri = new Uri(html).AbsoluteUri;
        string theme = _config.Theme == "light" ? "light" : "dark";
        return uri + "?theme=" + theme + "&lang=" + _config.EffectiveLanguage;
    }

    /// <summary>Switch the in-app screen (home / settings / history / collection) — no new window.</summary>
    public void NavigateTo(string page)
    {
        _pendingPage = page;
        if (_coreReady && Web.CoreWebView2 != null)
        {
            try { Web.CoreWebView2.Navigate(PageUrl(page)); } catch { }
        }
    }

    private void OnNavigated()
    {
        var src = Web.CoreWebView2?.Source ?? "";
        if (src.Contains("settings.html")) PushConfig();
        else if (src.Contains("history.html") || src.Contains("collection.html")) InjectData();
        else if (src.Contains("classifica.html")) { SubmitSelf(); InjectClassifica(); _ = PushWorld(); }
        else if (src.Contains("home.html")) { SubmitSelf(); InjectHome(); _ = PushHomeBoards(); PushNews(); }
        else if (src.Contains("welcome.html")) InjectWelcome();
    }

    private bool OnWelcome => (Web.CoreWebView2?.Source ?? "").Contains("welcome.html");

    // ================= guided setup (welcome.html) =================
    /// <summary>Everything the guided setup needs to start: current choices, screens, sync state.</summary>
    private void InjectWelcome()
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                theme = _config.Theme,
                lang = _config.EffectiveLanguage,
                langPref = _config.Language,
                volume = _config.Volume,
                startup = _config.StartWithWindows,
                checkUpdates = _config.CheckUpdates,
                shareCollection = _config.ShareCollection,
                gameFix = _config.GameOverlayFix,
                prepared = _config.OptimizedCount(),
                joined = _config.LeaderboardJoined,
                lbReady = _lb.Ready,
                monitor = _config.OverlayMonitor,
                screens = OverlayWindow.Screens(),
                syncing = _syncRunning,
                syncState = _config.SyncState,
                count = _store?.Count ?? 0,
                done = _config.OnboardingDone
            });
            Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraWizard && window.fanfaraWizard({json})");
        }
        catch { }
    }

    /// <summary>Step "Steam": is Steam open, which account, and can Fanfara read its achievements?</summary>
    private async System.Threading.Tasks.Task WizardCheckAsync()
    {
        bool steam = false;
        try { steam = System.Diagnostics.Process.GetProcessesByName("steam").Length > 0; } catch { }
        var r = await new SteamHistory(_config, _store, _games).CheckAsync();
        string? name = null, avatar = null;
        if (r.SteamId && _lb.Ready)
        {
            try
            {
                await (_games != null ? _lb.SubmitAsync(_games.PerfectCount, _games.TotalUnlocked, _games.PerfectGames()) : _lb.SubmitAsync(0, 0));   // makes sure the proxy knows the player
                var raw = await _lb.FriendsAsync();
                if (raw != null)
                {
                    using var d = JsonDocument.Parse(raw);
                    if (d.RootElement.TryGetProperty("friends", out var fr) && fr.ValueKind == JsonValueKind.Array)
                        foreach (var f in fr.EnumerateArray())
                            if (f.TryGetProperty("you", out var y) && y.ValueKind == JsonValueKind.True)
                            {
                                if (f.TryGetProperty("name", out var n)) name = n.GetString();
                                if (f.TryGetProperty("avatar", out var a)) avatar = a.GetString();
                                break;
                            }
                }
            }
            catch { }
        }
        var json = JsonSerializer.Serialize(new
        {
            steam,
            account = r.SteamId,
            name,
            avatar,
            privacy = r.Privacy == SteamHistory.Privacy.Public ? "public" : r.Privacy == SteamHistory.Privacy.Private ? "private" : "unknown",
            games = r.Games
        });
        try { if (OnWelcome) Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraWizardCheck && window.fanfaraWizardCheck({json})"); } catch { }
    }

    /// <summary>The app reports import progress: the guided setup's last step shows it live.</summary>
    public void PushSyncProgress(int done, int total, int added)
    {
        try
        {
            if (!OnWelcome) return;
            var st = JsonSerializer.Serialize(new { running = true, done, total, added });
            Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraWizardSync && window.fanfaraWizardSync({st})");
        }
        catch { }
    }

    private bool OnHome => (Web.CoreWebView2?.Source ?? "").Contains("home.html");

    /// <summary>Send the Home its local data (sblocchi, giochi, settimana, stile...) — instant and offline.</summary>
    private void InjectHome()
    {
        try
        {
            var json = HomeData.Build(_config, _store, _games, _lb.Ready, _syncRunning);
            Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraHome && window.fanfaraHome({json})");
        }
        catch { }
    }

    /// <summary>Then fetch world + friends rankings and hand them to the Home's Classifica tile.</summary>
    private async System.Threading.Tasks.Task PushHomeBoards()
    {
        if (!_lb.Ready) return;
        var world = _lb.WorldAsync();
        var friends = _lb.FriendsAsync();
        var rw = await world ?? "{\"ok\":false}";
        try { if (OnHome) Web.CoreWebView2?.ExecuteScriptAsync("window.fanfaraHomeBoard && window.fanfaraHomeBoard('world'," + rw + ")"); } catch { }
        var rf = await friends ?? "{\"ok\":false}";
        try { if (OnHome) Web.CoreWebView2?.ExecuteScriptAsync("window.fanfaraHomeBoard && window.fanfaraHomeBoard('friends'," + rf + ")"); } catch { }
    }

    /// <summary>Called by the app when a Steam sync starts/ends, so the Home can show it.</summary>
    public void SetSyncState(bool running)
    {
        _syncRunning = running;
        try
        {
            if (OnWelcome)
            {
                var ws = JsonSerializer.Serialize(new { running, state = _config.SyncState, count = _store?.Count ?? 0 });
                Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraWizardSync && window.fanfaraWizardSync({ws})");
                return;
            }
            if (!OnHome) return;
            var st = JsonSerializer.Serialize(new { running, lastSync = _config.LastSyncUtc });
            Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraHomeSync && window.fanfaraHomeSync({st})");
            if (!running) InjectHome();   // fresh numbers after the sync
        }
        catch { }
    }

    /// <summary>Register the user's current 100% score so they (and their friends) see it in the leaderboard.</summary>
    private void SubmitSelf()
    {
        try { if (_lb.Ready && _games != null) _ = _lb.SubmitAsync(_games.PerfectCount, _games.TotalUnlocked, _games.PerfectGames()); }
        catch { }
    }

    /// <summary>Send the local "100% games" cards + opt-in state to the Classifica screen.</summary>
    private void InjectClassifica()
    {
        try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraInit && window.fanfaraInit({BuildClassificaJson()})"); }
        catch { }
    }

    private string BuildClassificaJson()
    {
        var perfect = (_games?.PerfectGames() ?? new System.Collections.Generic.List<GameRecord>())
            .Select(g => new { appid = g.Appid, name = g.Name, ach = g.Total, date = g.LastUnlock });
        return JsonSerializer.Serialize(new
        {
            theme = _config.Theme,
            lang = _config.EffectiveLanguage,
            joined = _config.LeaderboardJoined,
            ready = _lb.Ready,
            perfect
        });
    }

    /// <summary>Fetch the world ranking from the proxy and hand the raw payload to the screen.</summary>
    private async System.Threading.Tasks.Task PushWorld()
    {
        var raw = await _lb.WorldAsync() ?? "{\"ok\":false}";
        try { Web.CoreWebView2?.ExecuteScriptAsync("window.fanfaraWorld && window.fanfaraWorld(" + raw + ")"); } catch { }
    }

    /// <summary>Fetch the friends ranking from the proxy and hand the raw payload to the screen.</summary>
    // Friends' unlocks per game for the back of the Collection cards. Cached for a while so flipping
    // through several cards of the same game costs a single proxy call.
    private readonly System.Collections.Generic.Dictionary<uint, (DateTime at, string json)> _friendsAch = new();
    private async System.Threading.Tasks.Task PushFriendsAch(uint appid)
    {
        string? raw = null;
        if (_friendsAch.TryGetValue(appid, out var hit) && DateTime.UtcNow - hit.at < TimeSpan.FromMinutes(30)) raw = hit.json;
        else if (_lb.Ready)
        {
            raw = await _lb.FriendsAchAsync(appid);
            if (raw != null && JsonTrue(raw, "ok")) _friendsAch[appid] = (DateTime.UtcNow, raw);
        }
        raw ??= "{\"ok\":false}";
        try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraFriendsAch && window.fanfaraFriendsAch({appid}, {raw})"); } catch { }
    }

    /// <summary>
    /// «Copy image» / «Post to the showcase» on an opened Collection card: the page hides its buttons and
    /// tells us where the card is; we snapshot the WebView, crop that area and either put it on the
    /// clipboard or send it to the Discord showcase through the proxy.
    /// </summary>
    private async System.Threading.Tasks.Task CaptureCardAsync(Msg m)
    {
        bool ok = false; string? error = null; string mode = m.mode == "share" ? "share" : "copy";
        try
        {
            if (Web.CoreWebView2 == null) throw new InvalidOperationException();
            using var ms = new MemoryStream();
            await Web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, ms);
            ms.Position = 0;
            var full = System.Windows.Media.Imaging.BitmapFrame.Create(ms,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

            // page coordinates (CSS px) -> captured pixels
            double s = full.PixelWidth / Math.Max(1.0, m.vw ?? full.PixelWidth);
            int x = (int)Math.Clamp(Math.Round((m.x ?? 0) * s), 0, full.PixelWidth - 1);
            int y = (int)Math.Clamp(Math.Round((m.y ?? 0) * s), 0, full.PixelHeight - 1);
            int w = (int)Math.Clamp(Math.Round((m.w ?? full.PixelWidth) * s), 1, full.PixelWidth - x);
            int h = (int)Math.Clamp(Math.Round((m.h ?? full.PixelHeight) * s), 1, full.PixelHeight - y);
            System.Windows.Media.Imaging.BitmapSource img =
                new System.Windows.Media.Imaging.CroppedBitmap(full, new System.Windows.Int32Rect(x, y, w, h));
            if (img.PixelWidth > 1000)
            {
                double k = 1000.0 / img.PixelWidth;
                img = new System.Windows.Media.Imaging.TransformedBitmap(img, new System.Windows.Media.ScaleTransform(k, k));
            }
            img.Freeze();

            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(img));
            byte[] png;
            using (var outMs = new MemoryStream()) { enc.Save(outMs); png = outMs.ToArray(); }

            if (mode == "copy")
            {
                // Bitmap + "PNG" format: Discord, WhatsApp, Paint and browsers all understand at least one.
                var data = new System.Windows.DataObject();
                data.SetImage(img);
                data.SetData("PNG", new MemoryStream(png));
                try { System.Windows.Clipboard.SetDataObject(data, true); }
                catch { System.Windows.Clipboard.SetImage(img); }
                ok = true;
            }
            else
            {
                var c = m.card;
                var card = new CardShare.Card(c?.appid ?? 0, c?.api ?? "", c?.name ?? "", c?.game ?? "", c?.p, c?.unlock ?? 0, c?.side ?? "front");
                var r = await CardShare.PostAsync(_config, png, card);
                ok = r.Ok; error = r.Error;
            }
        }
        catch { ok = false; error ??= "capture"; }
        var res = JsonSerializer.Serialize(new { ok, mode, error });
        try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraCardCaptured && window.fanfaraCardCaptured({res})"); } catch { }
    }

    private async System.Threading.Tasks.Task PushFriends()
    {
        var raw = await _lb.FriendsAsync() ?? "{\"ok\":false}";
        try { Web.CoreWebView2?.ExecuteScriptAsync("window.fanfaraFriends && window.fanfaraFriends(" + raw + ")"); } catch { }
    }

    private static bool JsonTrue(string raw, string prop)
    {
        try { using var d = JsonDocument.Parse(raw); return d.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True; }
        catch { return false; }
    }

    private void InjectData()
    {
        try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraInit && window.fanfaraInit({BuildDataJson()})"); }
        catch { }
    }

    /// <summary>The JSON payload the History/Collection screens consume (theme, language, records).</summary>
    private string BuildDataJson()
    {
        var items = (_store?.All() ?? new System.Collections.Generic.List<AchievementRecord>())
            .Select(r => new { name = r.Name, game = r.Game, desc = r.Desc, p = r.Percent, unlock = r.Unlock, api = r.Api, appid = r.Appid, icon = r.Icon });
        // Per-game progress (unlocked / total), name and minutes played: card backs and the Collection albums.
        var games = (_games?.All() ?? new System.Collections.Generic.List<GameRecord>())
            .Select(g => new { appid = g.Appid, u = g.Unlocked, t = g.Total, n = g.Name, h = g.Playtime });
        return JsonSerializer.Serialize(new { theme = _config.Theme, lang = _config.EffectiveLanguage, volume = _config.Volume, albumStyle = _config.AlbumStyle, albumFx = _config.AlbumFx, items, games });
    }

    /// <summary>Re-send data if the History/Collection screen is currently showing (after a live unlock or sync).</summary>
    public void RefreshData()
    {
        try
        {
            var src = Web.CoreWebView2?.Source ?? "";
            if (src.Contains("history.html") || src.Contains("collection.html")) InjectData();
            else if (src.Contains("classifica.html")) { InjectClassifica(); _ = PushWorld(); }
            else if (src.Contains("home.html")) InjectHome();
        }
        catch { }
    }

    /// <summary>Send the current config to the web layer (also used to re-sync a toggle on cancel).</summary>
    private void PushConfig()
    {
        var cfg = JsonSerializer.Serialize(new
        {
            style = _config.Style,
            sound = _config.Sound,
            position = _config.Position,
            duration = _config.DurationMs,
            volume = _config.Volume,
            startup = _config.StartWithWindows,
            checkUpdates = _config.CheckUpdates,
            shareCollection = _config.ShareCollection,
            gameOverlayFix = _config.GameOverlayFix,
            frameGenFix = _config.FrameGenFix,
            albumStyle = _config.AlbumStyle,
            albumFx = _config.AlbumFx,
            superFanfara = _config.SuperFanfara,
            friendAlerts = _config.FriendAlerts,
            friendSound = _config.FriendSound,
            theme = _config.Theme,
            language = _config.Language,
            osLang = Config.OsLang,
            version = VersionString()
        });
        Web.CoreWebView2.ExecuteScriptAsync($"window.initSettings && window.initSettings({cfg})");
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<Msg>(args.TryGetWebMessageAsString());
            if (msg == null) return;

            if (msg.action == "lb-world")
            {
                _ = PushWorld();
            }
            else if (msg.action == "sync")
            {
                SyncRequested?.Invoke();
            }
            else if (msg.action == "notif-read")
            {
                Notifications.MarkAllRead();
            }
            else if (msg.action == "news")
            {
                if (_lastNews != null) { _pendingNews = _lastNews; PushNews(); }
            }
            else if (msg.action == "update-now")
            {
                UpdateRequested?.Invoke();
            }
            else if (msg.action == "wz-check")
            {
                await WizardCheckAsync();
            }
            else if (msg.action == "wz-monitor")
            {
                if (!string.IsNullOrEmpty(msg.monitor)) { _config.OverlayMonitor = msg.monitor; _config.Save(); }
            }
            else if (msg.action == "wz-done")
            {
                _config.OnboardingDone = true; _config.Save();
                NavigateTo(string.IsNullOrEmpty(msg.page) ? "home.html" : msg.page);
            }
            else if (msg.action == "open-privacy")
            {
                // Steam's own privacy page, inside the Steam client if possible, else in the browser.
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = "steam://openurl/https://steamcommunity.com/my/edit/settings", UseShellExecute = true }); }
                catch
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        { FileName = "https://steamcommunity.com/my/edit/settings", UseShellExecute = true }); }
                    catch { }
                }
            }
            else if (msg.action == "open-profile")
            {
                var id = _config.ResolvedSteamId();
                if (id != 0)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        { FileName = $"https://steamcommunity.com/profiles/{id}", UseShellExecute = true }); }
                    catch { }
                }
            }
            else if (msg.action == "lb-friends")
            {
                _ = PushFriends();
            }
            else if (msg.action == "coll-friend" && !string.IsNullOrEmpty(msg.friend))
            {
                // The Collection page in "friend" mode asks for a friend's collection.
                var json = await new CollectionShare(_config, _store, _games).FriendAsync(msg.friend);
                try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraFriendColl && window.fanfaraFriendColl({json})"); } catch { }
            }
            else if (msg.action == "card-capture")
            {
                await CaptureCardAsync(msg);
            }
            else if (msg.action == "feedback")
            {
                var r = await Feedback.SendAsync(_config, msg.kind ?? "other", msg.text ?? "", msg.contact ?? "", msg.attachLog == true);
                var res = JsonSerializer.Serialize(new { ok = r.Ok, error = r.Error });
                try { Web.CoreWebView2?.ExecuteScriptAsync($"window.fanfaraFeedbackDone && window.fanfaraFeedbackDone({res})"); } catch { }
            }
            else if (msg.action == "friends-ach" && msg.appid is uint fa && fa != 0)
            {
                _ = PushFriendsAch(fa);
            }
            else if (msg.action == "lb-join")
            {
                int p = _games?.PerfectCount ?? 0, t = _games?.TotalUnlocked ?? 0;
                var raw = await _lb.JoinAsync(p, t) ?? "{\"ok\":false}";
                bool ok = JsonTrue(raw, "member");
                if (ok) { _config.LeaderboardJoined = true; _config.Save(); }
                try { Web.CoreWebView2?.ExecuteScriptAsync("window.fanfaraJoinState && window.fanfaraJoinState({joined:" + (ok ? "true" : "false") + "})"); } catch { }
                _ = PushWorld();
            }
            else if (msg.action == "lb-leave")
            {
                await _lb.LeaveAsync();
                _config.LeaderboardJoined = false; _config.Save();
                try { Web.CoreWebView2?.ExecuteScriptAsync("window.fanfaraJoinState && window.fanfaraJoinState({joined:false})"); } catch { }
                _ = PushWorld();
            }
            else if (msg.action == "test")
            {
                _overlay.ShowPreview(
                    msg.style ?? _config.Style,
                    msg.sound ?? _config.Sound,
                    msg.position ?? _config.Position,
                    0.6, Loc.S(_config.EffectiveLanguage, "preview"),
                    msg.volume ?? _config.Volume, Loc.S(_config.EffectiveLanguage, "previewDesc"));
            }
            else if (msg.action == "test-super" || msg.action == "test-friend")
            {
                // 0.6.0 «Prova»: the last game completed 100% (or any known game) as the example
                var g = _games.PerfectGames().FirstOrDefault() ?? _games.All().Where(x => x.Total > 0).OrderByDescending(x => x.LastUnlock).FirstOrDefault();
                uint appid = g?.Appid ?? 1145360; string name = g?.Name ?? "Hades"; int total = g?.Total ?? 49;
                int mine = _games.PerfectCount;
                if (msg.action == "test-super")
                    _overlay.ShowSuper(appid, name, total, g?.Playtime ?? 0, Math.Max(1, mine), _config, preview: true);
                else
                {
                    bool keep = _config.FriendSound;
                    if (msg.friendSound is bool tfs) _config.FriendSound = tfs;   // follow the switch even before Save
                    _overlay.ShowFriend("Fanfara", "", name, appid, mine + 1, mine, _config, preview: true);
                    _config.FriendSound = keep;
                }
            }
            else if (msg.action == "theme")
            {
                if (msg.theme != null) { _config.Theme = msg.theme; _config.Save(); ApplyTitleBar(msg.theme != "light"); }
            }
            else if (msg.action == "framegen")
            {
                if (msg.frameGenFix is bool fg)
                {
                    var lang = _config.EffectiveLanguage;
                    bool ok = await Mpo.SetDisabledAsync(fg);   // elevated reg.exe off the UI thread
                    if (ok)
                    {
                        _config.FrameGenFix = fg;
                        _config.Save();
                        _overlay.SetFrameGenFix(fg);   // start/stop the keep-on-top helper
                        var r = System.Windows.MessageBox.Show(
                            Loc.S(lang, "mpoRestartBody"), Loc.S(lang, "mpoRestartTitle"),
                            MessageBoxButton.YesNo, MessageBoxImage.Information);
                        if (r == MessageBoxResult.Yes)
                        {
                            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                { FileName = "shutdown", Arguments = "/r /t 5", UseShellExecute = true, CreateNoWindow = true }); }
                            catch { }
                        }
                    }
                    else
                    {
                        // UAC cancelled / failed — don't change anything, and re-sync the toggle.
                        System.Windows.MessageBox.Show(Loc.S(lang, "mpoCancelBody"), "Fanfara",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        PushConfig();
                    }
                }
            }
            else if (msg.action == "language")
            {
                if (msg.language != null)
                {
                    _config.Language = msg.language;
                    _config.Save();
                    _overlay.ApplyLanguage();   // live-update the overlay's language
                }
            }
            else if (msg.action == "save")
            {
                if (msg.style != null) _config.Style = msg.style;
                if (msg.sound != null) _config.Sound = msg.sound;
                if (msg.position != null) _config.Position = msg.position;
                if (msg.duration is int d && d > 0) _config.DurationMs = d;
                if (msg.volume is int vol && vol >= 0 && vol <= 100) _config.Volume = vol;
                if (msg.startup is bool s)
                {
                    _config.StartWithWindows = s;
                    StartupManager.Apply(s);
                }
                if (msg.checkUpdates is bool u) _config.CheckUpdates = u;
                if (msg.shareCollection is bool sc && sc != _config.ShareCollection)
                {
                    _config.ShareCollection = sc;
                    var share = new CollectionShare(_config, _store, _games);
                    if (sc) { _config.CollShareHash = ""; _ = share.UploadAsync(force: true); }
                    else _ = share.DeleteAsync();
                }
                if (msg.language != null) { _config.Language = msg.language; _overlay.ApplyLanguage(); }
                if (msg.albumStyle is "classic" or "ferro" or "reale" or "olografico" or "lacca" or "ottone") _config.AlbumStyle = msg.albumStyle;
                if (msg.albumFx is "full" or "lite" or "off") _config.AlbumFx = msg.albumFx;
                if (msg.superFanfara is bool sfan) _config.SuperFanfara = sfan;
                if (msg.friendAlerts is bool fal) _config.FriendAlerts = fal;
                if (msg.friendSound is bool fsnd) _config.FriendSound = fsnd;
                if (msg.gameOverlayFix is bool g && g != _config.GameOverlayFix)
                {
                    _config.GameOverlayFix = g;
                    if (g)
                    {
                        // Turned on: pre-prepare the library in the background.
                        System.Threading.Tasks.Task.Run(() => { try { SteamLibrary.PreoptimizeAll(_config); } catch { } });
                    }
                    else
                    {
                        // Turned off: undo the compatibility flag we set on games.
                        // ClearGameFixes() snapshots + clears both lists under the config lock,
                        // so a background pre-scan can't be mutating them while we iterate.
                        foreach (var exe in _config.ClearGameFixes()) FsOpt.Remove(exe);
                    }
                }
                _config.Save();
            }
        }
        catch { /* ignore malformed messages */ }
    }

    private static string VersionString()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new System.Version(0, 0, 0);
        return $"v{v.Major}.{v.Minor}.{(v.Build < 0 ? 0 : v.Build)}";
    }

    private class Msg
    {
        public string? action { get; set; }
        public string? style { get; set; }
        public string? sound { get; set; }
        public string? position { get; set; }
        public int? duration { get; set; }
        public int? volume { get; set; }
        public bool? startup { get; set; }
        public bool? checkUpdates { get; set; }
        public bool? gameOverlayFix { get; set; }
        public bool? frameGenFix { get; set; }
        public string? theme { get; set; }
        public string? language { get; set; }
        public uint? appid { get; set; }
        public string? kind { get; set; }
        public string? text { get; set; }
        public string? contact { get; set; }
        public bool? attachLog { get; set; }
        public string? mode { get; set; }
        public double? x { get; set; }
        public double? y { get; set; }
        public double? w { get; set; }
        public double? h { get; set; }
        public double? vw { get; set; }
        public CardMeta? card { get; set; }
        public string? monitor { get; set; }
        public string? page { get; set; }
        public string? friend { get; set; }
        public bool? shareCollection { get; set; }
        public string? albumStyle { get; set; }
        public string? albumFx { get; set; }
        public bool? superFanfara { get; set; }
        public bool? friendAlerts { get; set; }
        public bool? friendSound { get; set; }
    }

    private class CardMeta
    {
        public uint appid { get; set; }
        public string? api { get; set; }
        public string? name { get; set; }
        public string? game { get; set; }
        public double? p { get; set; }
        public long unlock { get; set; }
        public string? side { get; set; }
    }
}
