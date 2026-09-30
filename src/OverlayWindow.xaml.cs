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

        var html = Path.Combine(AppContext.BaseDirectory, "web", "notify.html");
        Web.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);
        Web.CoreWebView2.NavigationCompleted += (_, _) => _ready = true;
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
            durationMs = config.DurationMs
        };
        var json = JsonSerializer.Serialize(payload);
        Web.CoreWebView2.ExecuteScriptAsync($"window.fanfara.show({json})");
    }

    // ---- make the overlay window transparent to mouse clicks (click-through) ----
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }
}
