using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Fanfara;

/// <summary>
/// A plain WPF window that hosts a WebView2 page (web/{page}) and pushes a JSON payload into it
/// via window.fanfaraInit(...) once loaded. Used for the History and Collection windows, which
/// are HTML just like the Settings window. Call Refresh() to re-inject updated data.
/// </summary>
public class WebWindow : Window
{
    private readonly WebView2 _web = new();
    private readonly string _page;
    private readonly Func<string> _data;
    private readonly bool _dark;
    private bool _ready;

    public WebWindow(string title, string page, Func<string> data, bool dark, int width = 1000, int height = 760)
    {
        _page = page; _data = data; _dark = dark;
        Title = title;
        Width = width; Height = height;
        MinWidth = 760; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = System.Windows.Media.Brushes.Black;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/icon.ico", UriKind.Absolute)); }
        catch { /* no icon is fine */ }

        _web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(10, 14, 23);
        Content = _web;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Match the overlay/settings environment exactly: WebView2 allows only one set of
            // browser arguments per user-data folder per process.
            var opts = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
            };
            var env = await CoreWebView2Environment.CreateAsync(null, null, opts);
            await _web.EnsureCoreWebView2Async(env);

            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;

            _web.CoreWebView2.NavigationCompleted += (_, _) => { _ready = true; Inject(); };

            var html = Path.Combine(AppContext.BaseDirectory, "web", _page);
            _web.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);
        }
        catch { /* if WebView2 fails to start, the window simply shows empty */ }
    }

    private void Inject()
    {
        if (!_ready || _web.CoreWebView2 == null) return;
        try { _web.CoreWebView2.ExecuteScriptAsync($"window.fanfaraInit && window.fanfaraInit({_data()})"); }
        catch { }
    }

    /// <summary>Re-send current data to the page (e.g. after a new unlock or a sync).</summary>
    public void Refresh() => Inject();

    // ---- dark title bar to match the app theme ----
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            int v = _dark ? 1 : 0;
            DwmSetWindowAttribute(h, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
        }
        catch { }
    }
}
