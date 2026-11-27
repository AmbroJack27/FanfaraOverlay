using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Game pictures (0.6.1). Steam keeps the artwork of newer games in a hashed folder, so the classic
/// cdn.../steam/apps/&lt;appid&gt;/library_hero.jpg answers 404 for them (e.g. AC Black Flag Resynced).
/// Fanfara asks its proxy (/art) once per game where the pictures really are and remembers it in
/// %AppData%\Fanfara\art.json. Only the games that need it get an address; the others keep the classic one.
/// </summary>
public static class GameArt
{
    private class Rec
    {
        public string Hero { get; set; } = "";
        public string Header { get; set; } = "";
        public string Cap { get; set; } = "";
        public bool Hashed { get; set; }      // true = the classic address does not work for this game
        public long Ts { get; set; }          // when it was asked (unix seconds)
    }

    private static readonly object Gate = new();
    private static readonly string PathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fanfara", "art.json");
    private static readonly TimeSpan Keep = TimeSpan.FromDays(30);
    private static Dictionary<uint, Rec>? _map;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const string Cdn = "https://cdn.cloudflare.steamstatic.com/steam/apps/";

    private static Dictionary<uint, Rec> Map()
    {
        if (_map != null) return _map;
        try { if (File.Exists(PathFile)) _map = JsonSerializer.Deserialize<Dictionary<uint, Rec>>(File.ReadAllText(PathFile)); } catch { }
        return _map ??= new Dictionary<uint, Rec>();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
            File.WriteAllText(PathFile, JsonSerializer.Serialize(Map()));
        }
        catch { }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt"),
            $"{DateTime.Now:HH:mm:ss}  [immagini] {msg}{Environment.NewLine}"); } catch { }
    }

    /// <summary>The big picture of a game (Super Fanfara, album covers).</summary>
    public static string Hero(uint appid)
    {
        lock (Gate) { if (Map().TryGetValue(appid, out var r) && r.Hashed && r.Hero.Length > 0) return r.Hero; }
        return $"{Cdn}{appid}/library_hero.jpg";
    }

    /// <summary>The small horizontal picture of a game (the store header).</summary>
    public static string Header(uint appid)
    {
        lock (Gate) { if (Map().TryGetValue(appid, out var r) && r.Hashed && r.Header.Length > 0) return r.Header; }
        return $"{Cdn}{appid}/header.jpg";
    }

    /// <summary>The addresses the web pages need, as JSON: only the games whose pictures moved.</summary>
    public static string ScriptJson()
    {
        lock (Gate)
        {
            var o = Map().Where(kv => kv.Value.Hashed)
                .ToDictionary(kv => kv.Key.ToString(), kv => new { hero = kv.Value.Hero, header = kv.Value.Header, cap = kv.Value.Cap });
            return JsonSerializer.Serialize(o);
        }
    }

    /// <summary>
    /// Ask the proxy about the games not known yet (or asked more than 30 days ago).
    /// Returns true if some game got a new address (the pages should be refreshed).
    /// </summary>
    public static async Task<bool> EnsureAsync(Config config, IEnumerable<uint> appids)
    {
        var baseUrl = (config.SyncProxyUrl ?? "").TrimEnd('/');
        if (baseUrl.Length == 0) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        List<uint> todo;
        lock (Gate)
        {
            var m = Map();
            todo = appids.Where(a => a != 0).Distinct()
                .Where(a => !m.TryGetValue(a, out var r) || now - r.Ts > (long)Keep.TotalSeconds).ToList();
        }
        if (todo.Count == 0) return false;
        bool changed = false; int moved = 0;
        for (int i = 0; i < todo.Count; i += 50)
        {
            var batch = todo.Skip(i).Take(50).ToList();
            try
            {
                var raw = await Http.GetStringAsync($"{baseUrl}/art?appids={string.Join(",", batch)}");
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("art", out var art) || art.ValueKind != JsonValueKind.Object) continue;
                lock (Gate)
                {
                    var m = Map();
                    foreach (var a in batch)
                    {
                        var rec = new Rec { Ts = now };
                        if (art.TryGetProperty(a.ToString(), out var e) && e.ValueKind == JsonValueKind.Object)
                        {
                            rec.Hero = e.TryGetProperty("hero", out var h) ? h.GetString() ?? "" : "";
                            rec.Header = e.TryGetProperty("header", out var d) ? d.GetString() ?? "" : "";
                            rec.Cap = e.TryGetProperty("cap", out var c) ? c.GetString() ?? "" : "";
                            rec.Hashed = IsHashed(a, rec.Hero) || IsHashed(a, rec.Header);
                        }
                        m.TryGetValue(a, out var old);
                        if (rec.Hashed && (old == null || !old.Hashed || old.Hero != rec.Hero || old.Header != rec.Header)) { changed = true; moved++; }
                        m[a] = rec;
                    }
                }
            }
            catch (Exception ex) { Log($"errore: {ex.Message}"); return changed; }   // offline or old proxy: try again next time
        }
        lock (Gate) Save();
        if (moved > 0) Log($"{moved} giochi con le immagini in un indirizzo nuovo");
        return changed;
    }

    // ".../steam/apps/3751950/09d7.../library_hero.jpg?t=..." has one folder more than the classic address.
    private static bool IsHashed(uint appid, string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        var key = $"/steam/apps/{appid}/";
        int i = url.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return false;
        var rest = url[(i + key.Length)..];
        int q = rest.IndexOf('?'); if (q >= 0) rest = rest[..q];
        return rest.Contains('/');
    }
}
