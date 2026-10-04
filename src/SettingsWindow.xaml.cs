using System;
using System.IO;
using System.Text.Json;
using System.Windows;
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var env = await CoreWebView2Environment.CreateAsync();
        await Web.EnsureCoreWebView2Async(env);
        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.CoreWebView2.Settings.IsStatusBarEnabled = false;

        Web.CoreWebView2.WebMessageReceived += OnWebMessage;

        Web.CoreWebView2.NavigationCompleted += (_, _) =>
        {
            var cfg = JsonSerializer.Serialize(new
            {
                style = _config.Style,
                sound = _config.Sound,
                position = _config.Position,
                duration = _config.DurationMs,
                startup = _config.StartWithWindows,
                checkUpdates = _config.CheckUpdates,
                theme = _config.Theme
            });
            Web.CoreWebView2.ExecuteScriptAsync($"window.initSettings && window.initSettings({cfg})");
        };

        var html = Path.Combine(AppContext.BaseDirectory, "web", "settings.html");
        Web.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);
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
                    0.6, "Anteprima!");
            }
            else if (msg.action == "theme")
            {
                if (msg.theme != null) { _config.Theme = msg.theme; _config.Save(); }
            }
            else if (msg.action == "save")
            {
                if (msg.style != null) _config.Style = msg.style;
                if (msg.sound != null) _config.Sound = msg.sound;
                if (msg.position != null) _config.Position = msg.position;
                if (msg.duration is int d && d > 0) _config.DurationMs = d;
                if (msg.startup is bool s)
                {
                    _config.StartWithWindows = s;
                    StartupManager.Apply(s);
                }
                if (msg.checkUpdates is bool u) _config.CheckUpdates = u;
                _config.Save();
            }
        }
        catch { /* ignore malformed messages */ }
    }

    private class Msg
    {
        public string? action { get; set; }
        public string? style { get; set; }
        public string? sound { get; set; }
        public string? position { get; set; }
        public int? duration { get; set; }
        public bool? startup { get; set; }
        public bool? checkUpdates { get; set; }
        public string? theme { get; set; }
    }
}
