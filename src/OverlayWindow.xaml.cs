using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace Fanfara;

public partial class OverlayWindow : Window
{
    private readonly Config _config;
    private bool _ready;

    public OverlayWindow(Config config)
    {
        _config = config;
        InitializeComponent();

        // Never take focus / activation from the game or other windows.
        ShowActivated = false;

        // Cover the primary screen. (Multi-monitor handling can be added later.)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        Loaded += OnLoaded;
        SourceInitialized += (_, _) => MakeClickThrough();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Allow the overlay's notification sounds (WebAudio + the Fantasy style's sampled
        // stingers) to play without a user gesture — the overlay is never clicked.
        var opts = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
        };
        var env = await CoreWebView2Environment.CreateAsync(null, null, opts);
        await Web.EnsureCoreWebView2Async(env);

        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Web.CoreWebView2.Settings.AreDevToolsEnabled = false;

        Web.CoreWebView2.NavigationCompleted += (_, _) =>
        {
            _ready = true;
            ApplyLanguage();   // set the overlay's language as soon as the page is loaded
        };

        var html = Path.Combine(AppContext.BaseDirectory, "web", "notify.html");
        Web.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);

        // If the advanced Frame Generation fix is on, keep re-asserting top-most so the overlay
        // stays composited over FG / exclusive-fullscreen games.
        SetFrameGenFix(_config.FrameGenFix);
    }

    // Keeps DWM compositing our topmost window every frame, which (with MPO disabled) holds the
    // game in Composed Flip so the overlay stays visible even with Frame Generation.
    private System.Windows.Threading.DispatcherTimer? _keepTop;

    /// <summary>Start/stop the keep-on-top helper used by the advanced Frame Generation fix.</summary>
    public void SetFrameGenFix(bool on)
    {
        if (on)
        {
            if (_keepTop == null)
            {
                _keepTop = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _keepTop.Tick += (_, _) => ForceTopmost();
            }
            _keepTop.Start();
        }
        else _keepTop?.Stop();
    }

    /// <summary>Push the effective language (from config + OS) into the web layer.</summary>
    public void ApplyLanguage() => SetLanguage(_config.EffectiveLanguage);

    /// <summary>Tell the web layer which language to render notifications in.</summary>
    public void SetLanguage(string lang)
    {
        if (!_ready) return;
        Web.CoreWebView2.ExecuteScriptAsync($"window.fanfara && window.fanfara.setLang('{lang}')");
    }

    /// <summary>Ask the web layer to show a notification for an unlocked achievement.</summary>
    public void ShowAchievement(string name, double globalPercent, Config config)
    {
        if (!_ready) return;
        ForceTopmost();
        var payload = new
        {
            name,
            percent = globalPercent,     // web maps this to the rarity tier
            style = config.Style,        // "arcade" | "neon" | "loot" | "terminale" | ...
            sound = config.Sound,        // "classic" | "epico" | "levelup" | ...
            position = config.Position,  // bottom-center | top-center | center | top-right | ...
            durationMs = config.DurationMs,
            volume = config.Volume       // 0..100 notification sound volume
        };
        var json = JsonSerializer.Serialize(payload);
        Web.CoreWebView2.ExecuteScriptAsync($"window.fanfara.show({json})");
    }

    /// <summary>Preview with explicit settings (used by the settings window's "Prova" button).</summary>
    public void ShowPreview(string style, string sound, string position, double percent, string name, int volume = 100)
    {
        if (!_ready) return;
        ForceTopmost();
        var payload = new { name, percent, style, sound, position, durationMs = _config.DurationMs, volume };
        Web.CoreWebView2.ExecuteScriptAsync($"window.fanfara.show({JsonSerializer.Serialize(payload)})");
    }

    // ---- make the overlay window transparent to mouse clicks (click-through), 64-bit safe ----
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x20;
    private const long WS_EX_LAYERED = 0x80000;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // Re-assert top-most z-order without stealing focus — a game that took the foreground can
    // otherwise sit above our (also top-most) overlay. Called right before each notification.
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private void ForceTopmost()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
        catch { }
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }
}
