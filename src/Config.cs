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

    // ---- Achievement history / Collection sync (read-only, public Steam data via the proxy) ----
    // Rebuild the full unlocked-achievement history from Steam at first run / periodically.
    public bool SyncHistory { get; set; } = true;
    // Set true once the one-time backfill has completed, so we don't redo it every launch.
    public bool HistoryImported { get; set; } = false;
    // When the last successful sync finished (round-trip UTC). Used to space out quiet background refreshes.
    public string LastSyncUtc { get; set; } = "";
    // The Fanfara proxy (Cloudflare Worker) that holds the Steam Web API key. Public endpoint (not secret).
    public const string DefaultProxyUrl = "https://fanfara-steam.squall9171.workers.dev";
    public string SyncProxyUrl { get; set; } = DefaultProxyUrl;
    // Advanced override: force a specific SteamID64 instead of auto-detecting the signed-in user.
    public string SteamIdOverride { get; set; } = "";
    // Advanced override: use the user's own Steam Web API key directly (bypass the proxy). Empty = use proxy.
    public string SteamApiKey { get; set; } = "";

    // ---- Online leaderboard (100% completed games) ----
    // Opt-in: when true, the user appears in the public world leaderboard (name + avatar + score).
    // Off by default; the user turns it on from the Classifica screen, and can leave any time.
    public bool LeaderboardJoined { get; set; } = false;

    // Game .exe paths we've applied the fix to (so we don't re-notify, and can undo on disable).
    public List<string> OptimizedGames { get; set; } = new();
    // Game install folders already pre-scanned, so we don't re-scan the whole library every boot.
    public List<string> ScannedDirs { get; set; } = new();

    // Guards the two game-fix lists above AND their serialization in Save(). The background
    // library pre-scan (SteamLibrary.PreoptimizeAll) and the Steam worker's GAMEEXE handler
    // both mutate these lists from pool threads while the UI thread may be saving — enumerating
    // a List<string> while another thread adds to it throws / writes corrupt JSON. All access to
    // these lists goes through the thread-safe helpers below, which lock this gate.
    private readonly object _gate = new();

    /// <summary>Thread-safe: records a game .exe as optimized. Returns true if it was newly added.</summary>
    public bool AddOptimizedGame(string exe)
    {
        if (string.IsNullOrEmpty(exe)) return false;
        lock (_gate)
        {
            foreach (var s in OptimizedGames)
                if (string.Equals(s, exe, StringComparison.OrdinalIgnoreCase)) return false;
            OptimizedGames.Add(exe);
            return true;
        }
    }

    /// <summary>Thread-safe: records a game folder as scanned. Returns true if it was newly added.</summary>
    public bool AddScannedDir(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return false;
        lock (_gate)
        {
            foreach (var s in ScannedDirs)
                if (string.Equals(s, dir, StringComparison.OrdinalIgnoreCase)) return false;
            ScannedDirs.Add(dir);
            return true;
        }
    }

    /// <summary>Thread-safe: has this game folder already been scanned?</summary>
    public bool IsScanned(string dir)
    {
        lock (_gate)
        {
            foreach (var s in ScannedDirs)
                if (string.Equals(s, dir, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>Thread-safe: snapshots the optimized-game list and clears both game-fix lists
    /// (used when the overlay fix is turned off, so the caller can undo each exe). Returns the snapshot.</summary>
    public List<string> ClearGameFixes()
    {
        lock (_gate)
        {
            var snapshot = new List<string>(OptimizedGames);
            OptimizedGames.Clear();
            ScannedDirs.Clear();
            return snapshot;
        }
    }

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

    /// <summary>The SteamID64 to sync for: the manual override if set, else the signed-in Steam user. 0 if unknown.</summary>
    public ulong ResolvedSteamId()
    {
        if (!string.IsNullOrWhiteSpace(SteamIdOverride)
            && ulong.TryParse(SteamIdOverride.Trim(), out var o) && o > 0) return o;
        return SteamId.Current() ?? 0UL;
    }

    private static string Path_ =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(Path_))
            {
                var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(Path_)) ?? new Config();
                // Migrate old configs that still carry the pre-deploy placeholder URL.
                if (string.IsNullOrWhiteSpace(cfg.SyncProxyUrl) || cfg.SyncProxyUrl.Contains("REPLACE-ME"))
                    cfg.SyncProxyUrl = DefaultProxyUrl;
                return cfg;
            }
        }
        catch { /* fall back to defaults */ }
        return new Config();
    }

    private static readonly object _fileLock = new();

    public void Save()
    {
        try
        {
            // Serialize under _gate so the game-fix lists can't be mutated mid-enumeration by a
            // background thread, then write the file under a separate lock. Two locks avoid holding
            // _gate during slow disk I/O (which would block the background scan unnecessarily).
            string json;
            lock (_gate)
            {
                json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            }
            lock (_fileLock)
            {
                File.WriteAllText(Path_, json);
            }
        }
        catch { /* ignore */ }
    }
}
