using System;
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

    public SettingsWindow(Config config, OverlayWindow overlay)
    {
        _config = config;
        _overlay = overlay;
        InitializeComponent();
        Loaded += OnLoaded;
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
        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.CoreWebView2.Settings.IsStatusBarEnabled = false;

        Web.CoreWebView2.WebMessageReceived += OnWebMessage;

        Web.CoreWebView2.NavigationCompleted += (_, _) => PushConfig();

        var html = Path.Combine(AppContext.BaseDirectory, "web", "settings.html");
        Web.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);
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
            gameOverlayFix = _config.GameOverlayFix,
            frameGenFix = _config.FrameGenFix,
            theme = _config.Theme,
            language = _config.Language,
            osLang = Config.OsLang,
            version = VersionString()
        });
        Web.CoreWebView2.ExecuteScriptAsync($"window.initSettings && window.initSettings({cfg})");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<Msg>(args.TryGetWebMessageAsString());
            if (msg == null) return;

            if (msg.action == "test")
            {
                _overlay.ShowPreview(
                    msg.style ?? _config.Style,
                    msg.sound ?? _config.Sound,
                    msg.position ?? _config.Position,
                    0.6, Loc.S(_config.EffectiveLanguage, "preview"),
                    msg.volume ?? _config.Volume);
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
                    bool ok = Mpo.SetDisabled(fg);
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
                if (msg.language != null) { _config.Language = msg.language; _overlay.ApplyLanguage(); }
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
                        foreach (var exe in _config.OptimizedGames) FsOpt.Remove(exe);
                        _config.OptimizedGames.Clear();
                        _config.ScannedDirs.Clear();
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
    }
}
