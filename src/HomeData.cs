using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Fanfara;

/// <summary>
/// Builds the JSON payload for the Home screen (home.html → window.fanfaraHome(...)).
/// Everything comes from local stores, so the Home fills instantly and offline; the online
/// leaderboard is pushed separately afterwards (see SettingsWindow.PushHomeBoards).
/// </summary>
public static class HomeData
{
    public static string Build(Config config, AchievementStore? store, GamesStore? games, bool leaderboardReady, bool syncing)
    {
        var all = store?.All() ?? new List<AchievementRecord>();          // most recent first
        var gameList = games?.All() ?? new List<GameRecord>();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Live-captured unlocks may not know the game's name yet: borrow it from the games store.
        var gameNames = new Dictionary<uint, string>();
        foreach (var g in gameList) if (!string.IsNullOrEmpty(g.Name)) gameNames[g.Appid] = g.Name;
        string GameOf(AchievementRecord r) =>
            !string.IsNullOrEmpty(r.Game) ? r.Game : (gameNames.TryGetValue(r.Appid, out var n) ? n : "");

        object Card(AchievementRecord r) => new
        {
            name = r.Name, game = GameOf(r), p = r.Percent, unlock = r.Unlock, icon = r.Icon, appid = r.Appid
        };

        // Last 3 unlocks (Cronologia tile).
        var recent = all.Where(r => r.Unlock > 0).Take(3).Select(Card).ToList();

        // Unlock times of the last 8 days: the page buckets them per local day (week chart, "last 24h").
        var times = all.Where(r => r.Unlock > now - 8 * 86400).Select(r => r.Unlock).Take(5000).ToList();

        // Rarest achievements (Collezione tile) and how many are legendary (< 1% of players).
        var rarest = all.Where(r => r.Percent != null).OrderBy(r => r.Percent).Take(3).Select(Card).ToList();
        int legendary = all.Count(r => r.Percent is double p && p < 1.0);

        // "Ci sei quasi": unfinished games at least half done, fewest achievements left first.
        var almost = gameList
            .Where(g => g.Total > 0 && g.Unlocked < g.Total && g.Unlocked * 2 >= g.Total)
            .OrderBy(g => g.Total - g.Unlocked)
            .ThenByDescending(g => (double)g.Unlocked / g.Total)
            .Take(3)
            .Select(g => new { appid = g.Appid, name = g.Name, u = g.Unlocked, t = g.Total })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            theme = config.Theme,
            lang = config.EffectiveLanguage,
            style = config.Style,
            position = config.Position,
            duration = config.DurationMs,
            volume = config.Volume,
            total = all.Count,
            perfect = games?.PerfectCount ?? 0,
            gamesPlayed = gameList.Count(g => g.Unlocked > 0),
            legendary,
            recent,
            times,
            rarest,
            almost,
            lbReady = leaderboardReady,
            joined = config.LeaderboardJoined,
            lastSync = config.LastSyncUtc,
            syncing
        });
    }
}
