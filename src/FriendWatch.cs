using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>A Steam friend who uses Fanfara just completed a game 100%.</summary>
public record FriendPerfect(string SteamId, string Who, string Avatar, uint Appid, string Game, int Theirs);

/// <summary>
/// Friends at 100% (0.6.0). Every few minutes reads the Friends leaderboard (which, from 0.6.0, also lists
/// the games each friend has at 100%) and reports the games that were not there last time.
/// The first time a friend is seen — or the first time their list arrives — it is only remembered, so
/// nobody gets a flood of old completions. Kept in %AppData%\Fanfara\friends-perf.json.
/// </summary>
public static class FriendWatch
{
    private static readonly string PathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fanfara", "friends-perf.json");
    private static readonly TimeSpan Fresh = TimeSpan.FromDays(7);   // older completions are never announced

    private static void Log(string msg)
    {
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt"),
            $"{DateTime.Now:HH:mm:ss}  [amici100] {msg}{Environment.NewLine}"); } catch { }
    }

    private static Dictionary<string, List<uint>> Load()
    {
        try { if (File.Exists(PathFile)) return JsonSerializer.Deserialize<Dictionary<string, List<uint>>>(File.ReadAllText(PathFile)) ?? new(); }
        catch { }
        return new();
    }

    private static void Save(Dictionary<string, List<uint>> d)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!); File.WriteAllText(PathFile, JsonSerializer.Serialize(d)); } catch { }
    }

    public static async Task<List<FriendPerfect>> CheckAsync(Config config)
    {
        var news = new List<FriendPerfect>();
        var lb = new Leaderboard(config);
        if (!lb.Ready) return news;
        var raw = await lb.FriendsAsync();
        if (string.IsNullOrEmpty(raw)) return news;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("friends", out var arr) || arr.ValueKind != JsonValueKind.Array) return news;
            var seen = Load();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), oldest = now - (long)Fresh.TotalSeconds;
            foreach (var f in arr.EnumerateArray())
            {
                if (f.TryGetProperty("you", out var you) && you.ValueKind == JsonValueKind.True) continue;
                string sid = f.TryGetProperty("steamid", out var s) ? s.GetString() ?? "" : "";
                if (sid.Length == 0 || !f.TryGetProperty("perf", out var perf) || perf.ValueKind != JsonValueKind.Array) continue;
                var list = new List<(uint appid, string name, long ts)>();
                foreach (var g in perf.EnumerateArray())
                {
                    if (g.ValueKind != JsonValueKind.Array || g.GetArrayLength() < 1) continue;
                    uint a = g[0].TryGetUInt32(out var av) ? av : 0;
                    if (a == 0) continue;
                    list.Add((a, g.GetArrayLength() > 1 ? g[1].GetString() ?? "" : "", g.GetArrayLength() > 2 && g[2].TryGetInt64(out var t) ? t : 0));
                }
                // first sight of this friend, or of their list: just remember it
                if (!seen.TryGetValue(sid, out var known)) { seen[sid] = new List<uint> { 0 }.Concat(list.Select(x => x.appid)).ToList(); continue; }   // 0 = "list received"
                string who = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string avatar = f.TryGetProperty("avatar", out var av2) ? av2.GetString() ?? "" : "";
                int theirs = f.TryGetProperty("perfect", out var p) && p.TryGetInt32(out var pv) ? pv : list.Count;
                foreach (var x in list)
                {
                    if (known.Contains(x.appid)) continue;
                    known.Add(x.appid);
                    if (x.ts > 0 && x.ts < oldest) continue;   // an old game, just arrived late: no alert
                    news.Add(new FriendPerfect(sid, who, avatar, x.appid, x.name, theirs));
                }
            }
            Save(seen);
            if (news.Count > 0) Log($"{news.Count} nuovi giochi al 100% degli amici");
        }
        catch (Exception ex) { Log($"errore: {ex.Message}"); }
        return news;
    }
}
