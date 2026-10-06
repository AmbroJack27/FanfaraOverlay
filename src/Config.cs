using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Fanfara;

/// <summary>User preferences, loaded from config.json next to the .exe.</summary>
public class Config
{
    public string Style { get; set; } = "arcade";     // arcade | neon | loot | terminale | scudo | ...
    public string Sound { get; set; } = "classic";    // classic | epico | levelup | ...
    // Where the notification appears on screen:
    // bottom-center | top-center | center | top-right | bottom-right | top-left | bottom-left
    public string Position { get; set; } = "bottom-center";
    public int DurationMs { get; set; } = 4200;
    public int Volume { get; set; } = 100;             // notification sound volume, 0..100
    public int PollMs { get; set; } = 1000;            // how often to check Steam for new unlocks
    public bool StartWithWindows { get; set; } = false; // launch automatically when Windows starts
    public string Theme { get; set; } = "dark";        // settings window appearance: "dark" | "light"
    public bool CheckUpdates { get; set; } = true;      // check GitHub for a newer version at startup
    // UI/notification language: "auto" (follow Windows) | it | en | fr | de | es
    public string Language { get; set; } = "auto";
    // Auto-disable Windows "fullscreen optimizations" for detected games, so the overlay
    // shows over borderless/fullscreen titles. Per-user, no admin, reversible.
    public bool GameOverlayFix { get; set; } = true;
    // Advanced: force DWM Composed Flip (disable MPO) so the overlay also shows over games
    // that use Frame Generation / exclusive fullscreen. Machine-wide, needs admin + a reboot.
    public bool FrameGenFix { get; set; } = false;
    // Game .exe paths we've applied the fix to (so we don't re-notify, and can undo on disable).
    public List<string> OptimizedGames { get; set; } = new();
    // Game install folders already pre-scanned, so we don't re-scan the whole library every boot.
    public List<string> ScannedDirs { get; set; } = new();

    /// <summary>Supported interface languages.</summary>
    public static readonly string[] Languages = { "it", "en", "fr", "de", "es" };

    /// <summary>The two-letter code of the current Windows UI language (e.g. "it", "de").</summary>
    public static string OsLang =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>
    /// Resolve an effective language from a preference ("auto" or a code) and the OS language.
    /// Mirrors resolveLang() in web/i18n.js so C# and the web layer always agree.
    /// </summary>
    public static string ResolveLang(string? pref, string? os)
    {
        if (!string.IsNullOrEmpty(pref) && pref != "auto" && Array.IndexOf(Languages, pref) >= 0)
            return pref!;
        os = (os ?? "").Trim().ToLowerInvariant();
        if (os.Length > 2) os = os.Substring(0, 2);
        return Array.IndexOf(Languages, os) >= 0 ? os : "en";
    }

    /// <summary>The language actually in effect right now (preference resolved against the OS).</summary>
    public string EffectiveLanguage => ResolveLang(Language, OsLang);

    private static string Path_ =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(Path_))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(Path_)) ?? new Config();
        }
        catch { /* fall back to defaults */ }
        return new Config();
    }

    private static readonly object _saveLock = new();

    public void Save()
    {
        try
        {
            lock (_saveLock)
            {
                File.WriteAllText(Path_,
                    JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { /* ignore */ }
    }
}
