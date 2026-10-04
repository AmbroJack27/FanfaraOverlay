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
        var env = await CoreWebView2Environment.CreateAsync();
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
        var payload = new
        {
            name,
            percent = globalPercent,     // web maps this to the rarity tier
            style = config.Style,        // "arcade" | "neon" | "loot" | "terminale" | ...
            sound = config.Sound,        // "classic" | "epico" | "levelup" | ...
            position = config.Position,  // bottom-center | top-center | center | top-right | ...
            durationMs = config.DurationMs
        };
        var json = JsonSerializer.Serialize(payload);
        Web.CoreWebView2.ExecuteScriptAsync($"window.fanfara.show({json})");
    }

    /// <summary>Preview with explicit settings (used by the settings window's "Prova" button).</summary>
    public void ShowPreview(string style, string sound, string position, double percent, string name)
    {
        if (!_ready) return;
        var payload = new { name, percent, style, sound, position, durationMs = _config.DurationMs };
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

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }
}
