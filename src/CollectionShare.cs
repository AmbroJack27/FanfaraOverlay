using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Friends' collections. After a sync the app sends a compact, gzipped copy of the player's
/// Collection to the Fanfara proxy (only if "Show my collection to friends" is on), and it can
/// fetch the collection of a Steam friend who also uses Fanfara to show it in the Collection page.
///
/// Compact format (JSON, then gzip, then base64):
///   { v:1, g:[[appid, name, unlocked, total, minutesPlayed]], a:[[appid, api, name, desc, unlock, percent, icon]] }
/// where icon is just the file name when it is a standard Steam icon URL (rebuilt on the other side).
/// </summary>
public class CollectionShare
{
    private const string IconBase = "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(40) };

    private readonly Config _config;
    private readonly AchievementStore _store;
    private readonly GamesStore _games;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public CollectionShare(Config config, AchievementStore store, GamesStore games)
    { _config = config; _store = store; _games = games; }

    private string Base => (_config.SyncProxyUrl ?? "").TrimEnd('/');
    private bool Ready => !string.IsNullOrEmpty(Base) && !Base.Contains("REPLACE-ME") && _config.ResolvedSteamId() != 0;

    private static void Log(string msg)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss}  [amici] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>
    /// Sends my collection to the proxy if sharing is on and something changed since the last upload.
    /// Safe to call often: it does nothing when the collection is unchanged.
    /// </summary>
    public async Task UploadAsync(bool force = false, CancellationToken ct = default)
    {
        if (!_config.ShareCollection || !Ready) return;
        if (!await Gate.WaitAsync(0, ct)) return;   // an upload is already running
        try
        {
            var all = _store.All();
            if (all.Count == 0) return;
            var json = BuildCompact(all, _games.All());
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
            if (!force && hash == _config.CollShareHash) return;   // nothing new to send

            string data = Convert.ToBase64String(Gzip(json));
            var body = JsonSerializer.Serialize(new { steamid = _config.ResolvedSteamId().ToString(), cards = all.Count, data });
            using var resp = await Http.PostAsync($"{Base}/coll/put", new StringContent(body, Encoding.UTF8, "application/json"), ct);
            var txt = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode && txt.Contains("\"ok\":true") && !txt.Contains("throttled"))
            {
                _config.CollShareHash = hash;
                _config.Save();
                Log($"collezione condivisa: {all.Count} carte, {data.Length / 1024} KB");
            }
            else Log($"condivisione non riuscita: {(int)resp.StatusCode} {txt}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log($"errore condivisione: {ex.Message}"); }
        finally { Gate.Release(); }
    }

    /// <summary>Stops sharing: deletes my collection from the proxy.</summary>
    public async Task DeleteAsync(CancellationToken ct = default)
    {
        if (!Ready) return;
        try
        {
            var body = JsonSerializer.Serialize(new { steamid = _config.ResolvedSteamId().ToString() });
            using var resp = await Http.PostAsync($"{Base}/coll/del", new StringContent(body, Encoding.UTF8, "application/json"), ct);
            _config.CollShareHash = "";
            _config.Save();
            Log($"condivisione disattivata ({(int)resp.StatusCode})");
        }
        catch (Exception ex) { Log($"errore disattivazione: {ex.Message}"); }
    }

    /// <summary>
    /// A friend's collection, as the JSON the Collection page understands:
    /// { ok, name, avatar, ts, items:[{name,game,desc,p,unlock,api,appid,icon}], games:[{appid,u,t,n,h}] }
    /// or { ok:false, error } (none | notfriend | friendsPrivate | net).
    /// </summary>
    public async Task<string> FriendAsync(string friendId, CancellationToken ct = default)
    {
        if (!Ready) return "{\"ok\":false,\"error\":\"net\"}";
        if (!ulong.TryParse(friendId, out _)) return "{\"ok\":false,\"error\":\"friend\"}";
        try
        {
            var raw = await Http.GetStringAsync($"{Base}/coll/get?steamid={_config.ResolvedSteamId()}&friend={friendId}", ct);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            {
                string err = root.TryGetProperty("error", out var e) ? e.GetString() ?? "net" : "net";
                return JsonSerializer.Serialize(new { ok = false, error = err });
            }
            string data = root.GetProperty("data").GetString() ?? "";
            string compact = Gunzip(Convert.FromBase64String(data));
            return Expand(compact,
                root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                root.TryGetProperty("avatar", out var av) ? av.GetString() ?? "" : "",
                root.TryGetProperty("ts", out var ts) ? ts.GetInt64() : 0);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Log($"errore lettura collezione amico: {ex.Message}"); return "{\"ok\":false,\"error\":\"net\"}"; }
    }

    // ---- compact format ----

    internal static string BuildCompact(List<AchievementRecord> all, List<GameRecord> games)
    {
        var g = new List<object[]>();
        foreach (var x in games) g.Add(new object[] { x.Appid, x.Name, x.Unlocked, x.Total, x.Playtime });
        var a = new List<object?[]>();
        foreach (var r in all)
        {
            string icon = r.Icon ?? "";
            string prefix = IconBase + r.Appid + "/";
            if (icon.StartsWith(prefix, StringComparison.Ordinal)) icon = icon.Substring(prefix.Length);
            a.Add(new object?[] { r.Appid, r.Api, r.Name, r.Desc, r.Unlock, r.Percent.HasValue ? Math.Round(r.Percent.Value, 2) : null, icon });
        }
        return JsonSerializer.Serialize(new { v = 1, g, a });
    }

    internal static string Expand(string compact, string name, string avatar, long ts)
    {
        using var doc = JsonDocument.Parse(compact);
        var root = doc.RootElement;
        var gameName = new Dictionary<uint, string>();
        var games = new List<object>();
        foreach (var x in root.GetProperty("g").EnumerateArray())
        {
            uint appid = x[0].GetUInt32();
            string n = x[1].GetString() ?? "";
            gameName[appid] = n;
            games.Add(new { appid, n, u = x[2].GetInt32(), t = x[3].GetInt32(), h = x[4].GetInt32() });
        }
        var items = new List<object>();
        foreach (var x in root.GetProperty("a").EnumerateArray())
        {
            uint appid = x[0].GetUInt32();
            string icon = x[6].GetString() ?? "";
            if (icon.Length > 0 && !icon.StartsWith("http", StringComparison.OrdinalIgnoreCase)) icon = IconBase + appid + "/" + icon;
            double? p = x[5].ValueKind == JsonValueKind.Number ? x[5].GetDouble() : null;
            items.Add(new
            {
                appid,
                api = x[1].GetString() ?? "",
                name = x[2].GetString() ?? "",
                desc = x[3].GetString() ?? "",
                unlock = x[4].GetInt64(),
                p,
                icon,
                game = gameName.TryGetValue(appid, out var gn) ? gn : ""
            });
        }
        return JsonSerializer.Serialize(new { ok = true, name, avatar, ts, items, games });
    }

    private static byte[] Gzip(string s)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var b = Encoding.UTF8.GetBytes(s);
            gz.Write(b, 0, b.Length);
        }
        return ms.ToArray();
    }

    private static string Gunzip(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var gz = new GZipStream(ms, CompressionMode.Decompress);
        using var sr = new StreamReader(gz, Encoding.UTF8);
        return sr.ReadToEnd();
    }
}
