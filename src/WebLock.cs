using Microsoft.Web.WebView2.Wpf;

namespace Fanfara;

/// <summary>
/// Makes a WebView2 behave like an app window instead of a browser: the page always shows at
/// 100% (no Ctrl + / Ctrl − / Ctrl + wheel or pinch zoom, which made everything tiny or huge),
/// no F12 developer tools, and no browser shortcuts such as F5 reload, Ctrl+F find or Ctrl+P print.
/// Copy, paste and the other editing keys keep working in text fields.
/// </summary>
internal static class WebLock
{
    public static void Apply(WebView2 web)
    {
        var s = web.CoreWebView2.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.IsStatusBarEnabled = false;
        s.AreDevToolsEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsPinchZoomEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;

        // Undo any zoom left over from before this version, and keep it at 100%.
        web.ZoomFactor = 1.0;
        web.ZoomFactorChanged += (_, _) =>
        {
            if (Math.Abs(web.ZoomFactor - 1.0) > 0.001) web.ZoomFactor = 1.0;
        };
    }
}
