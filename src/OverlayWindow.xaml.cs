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

        // Cover the primary screen. Its top-left corner is always (0,0); the virtual-screen origin is NOT,
        // when another monitor sits to the left of / above the primary one (that put the pop-ups there).
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        Loaded += OnLoaded;
        SourceInitialized += (_, _) => { MakeClickThrough(); PlaceOn(PrimaryMonitor()); };
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

        WebLock.Apply(Web);   // no zoom, no F12, no browser shortcuts

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
    // The keep-on-top helper runs ONLY while a notification is on screen — not continuously.
    // Re-asserting top-most every frame during normal play caused micro-stutter with Frame Generation.
    private System.Windows.Threading.DispatcherTimer? _keepTop;
    private bool _frameGenFix;
    private DateTime _keepTopUntil;

    /// <summary>Enable/disable the advanced Frame Generation fix.</summary>
    public void SetFrameGenFix(bool on)
    {
        _frameGenFix = on;
        if (!on) _keepTop?.Stop();
    }

    /// <summary>While the FG fix is on, keep the overlay on top for one notification's lifetime, then stop.</summary>
    private void PulseKeepTop(int durationMs)
    {
        if (!_frameGenFix) return;
        _keepTopUntil = DateTime.UtcNow.AddMilliseconds(durationMs + 1500);
        if (_keepTop == null)
        {
            _keepTop = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _keepTop.Tick += (_, _) =>
            {
                ForceTopmost();
                if (DateTime.UtcNow >= _keepTopUntil) _keepTop!.Stop();
            };
        }
        if (!_keepTop.IsEnabled) _keepTop.Start();
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
    public void ShowAchievement(string name, double globalPercent, string desc, Config config)
    {
        if (!_ready) return;
        bool moved = PlaceOn(TargetMonitor());   // the screen where the game is (or the one chosen)
        ForceTopmost();
        PulseKeepTop(config.DurationMs);   // keep on top only while this notification is visible
        LogShown(name);
        var payload = new
        {
            name,
            desc,                        // the achievement's description (what you unlocked)
            percent = globalPercent,     // web maps this to the rarity tier
            style = config.Style,        // "arcade" | "neon" | "loot" | "terminale" | ...
            sound = config.Sound,        // "classic" | "epico" | "levelup" | ...
            position = config.Position,  // bottom-center | top-center | center | top-right | ...
            durationMs = config.DurationMs,
            volume = config.Volume       // 0..100 notification sound volume
        };
        var json = JsonSerializer.Serialize(payload);
        RunShow($"window.fanfara.show({json})", moved);
    }

    /// <summary>Run the show script; after a move to another screen, give the page a moment to take the new size.</summary>
    private async void RunShow(string script, bool moved)
    {
        try
        {
            if (moved) await System.Threading.Tasks.Task.Delay(180);
            await Web.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }

    // ---- which screen: the one with the game (foreground window), or the one chosen in the guided setup ----
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }
    private delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFOEX mi);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    private const uint MONITOR_DEFAULTTOPRIMARY = 1, MONITOR_DEFAULTTONEAREST = 2, MONITORINFOF_PRIMARY = 1;

    /// <summary>A screen as the guided setup shows it (physical pixels, Windows device name as id).</summary>
    public record ScreenInfo(string id, bool primary, int x, int y, int w, int h);

    private static MONITORINFOEX Info(IntPtr hMon)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = "" };
        GetMonitorInfo(hMon, ref mi);
        return mi;
    }

    /// <summary>Every screen connected right now.</summary>
    public static System.Collections.Generic.List<ScreenInfo> Screens()
    {
        var list = new System.Collections.Generic.List<ScreenInfo>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr dc, ref RECT r, IntPtr d) =>
            {
                var mi = Info(h);
                list.Add(new ScreenInfo(mi.szDevice ?? "", (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top));
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return list;
    }

    private static IntPtr PrimaryMonitor() => MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);

    private IntPtr TargetMonitor()
    {
        try
        {
            var pref = _config.OverlayMonitor ?? "auto";
            if (pref != "auto")
            {
                IntPtr found = IntPtr.Zero;
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr dc, ref RECT r, IntPtr d) =>
                {
                    if (string.Equals(Info(h).szDevice, pref, StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
                    return true;
                }, IntPtr.Zero);
                if (found != IntPtr.Zero) return found;   // the chosen screen was unplugged: fall back to "auto"
            }
            var fg = GetForegroundWindow();
            return fg != IntPtr.Zero ? MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST) : PrimaryMonitor();
        }
        catch { return PrimaryMonitor(); }
    }

    private RECT _placed;
    /// <summary>Cover a whole screen with the (transparent, click-through) overlay. Returns true if it moved.</summary>
    private bool PlaceOn(IntPtr hMon)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || hMon == IntPtr.Zero) return false;
            var r = Info(hMon).rcMonitor;
            if (r.Right <= r.Left || r.Bottom <= r.Top) return false;
            if (r.Left == _placed.Left && r.Top == _placed.Top && r.Right == _placed.Right && r.Bottom == _placed.Bottom) return false;
            _placed = r;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, w, h, SWP_NOACTIVATE);
            // Moving to a screen with a different scaling makes WPF resize the window for the new DPI:
            // set the exact rectangle once more after that has happened.
            Dispatcher.BeginInvoke(new Action(() => SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, w, h, SWP_NOACTIVATE)),
                System.Windows.Threading.DispatcherPriority.Background);
            return true;
        }
        catch { return false; }
    }

    // ---- diagnostics: where was the pop-up shown, and where was the game? (helps "I heard it but saw nothing") ----
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    private static readonly string LogPath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");
    private void LogShown(string name)
    {
        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var fg = System.Windows.Forms.Screen.FromHandle(GetForegroundWindow());
            int quns = SHQueryUserNotificationState(out var st) == 0 ? st : -1;   // 3 = exclusive fullscreen
            string line = $"[overlay] notifica '{name}' · schermi {screens.Length} · gioco su {(fg.Primary ? "principale" : "secondario")} {fg.Bounds.Width}x{fg.Bounds.Height}"
                        + $" · overlay {Left},{Top} {Width}x{Height} · stato Windows {quns}{(quns == 3 ? " (schermo intero esclusivo)" : "")}";
            line += $" · schermo scelto {_config.OverlayMonitor} · overlay su {_placed.Left},{_placed.Top} {_placed.Right - _placed.Left}x{_placed.Bottom - _placed.Top}";
            // The Steam worker writes to the same file at the same moment: retry briefly instead of losing the line.
            for (int i = 0; i < 5; i++)
            {
                try { System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {line}{Environment.NewLine}"); break; }
                catch (IOException) { System.Threading.Thread.Sleep(40); }
            }
        }
        catch { }
    }

    /// <summary>Preview with explicit settings (used by the settings window's "Prova" button).</summary>
    public void ShowPreview(string style, string sound, string position, double percent, string name, int volume = 100, string desc = "")
    {
        if (!_ready) return;
        bool moved = PlaceOn(TargetMonitor());   // "auto" = the screen with Fanfara's own window, when testing from it
        ForceTopmost();
        PulseKeepTop(_config.DurationMs);   // keep on top only while this preview is visible
        // preview = true: pressing "Test" again replaces the running test instead of queueing another one
        var payload = new { name, desc, percent, style, sound, position, durationMs = _config.DurationMs, volume, preview = true };
        RunShow($"window.fanfara.show({JsonSerializer.Serialize(payload)})", moved);
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
