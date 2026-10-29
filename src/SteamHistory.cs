using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Rebuilds the full unlocked-achievement history from Steam, through the Fanfara proxy
/// (which holds the Web API key). Read-only, public data. Needs the Steam profile's
/// "Game details" to be public. Live pop-ups do NOT depend on this — it only fills the
/// History/Collection with what was unlocked before Fanfara was installed.
/// </summary>
public class SteamHistory
{
    public record Progress(int DoneGames, int TotalGames, int NewAchievements);
    public enum Result { Ok, ProfilePrivate, NoSteamId, NoProxy, Error }

    /// <summary>How many achievements were newly added by the last BackfillAsync run.</summary>
    public int LastAdded { get; private set; }

    private readonly Config _config;
    private readonly AchievementStore _store;
    private readonly GamesStore _games;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly string LogPath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");
    private static void Log(string m)
    {
        try { System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  [sync] {m}{Environment.NewLine}"); } catch { }
    }

    public SteamHistory(Config config, AchievementStore store, GamesStore games) { _config = config; _store = store; _games = games; }

    private string Base => (_config.SyncProxyUrl ?? "").TrimEnd('/');

    // Steam's Web API wants the full language name (e.g. "italian"), not the 2-letter code.
    private static string SteamLang(string code) => code switch
    {
        "it" => "italian",
        "fr" => "french",
        "de" => "german",
        "es" => "spanish",
        _ => "english",
    };

    /// <summary>
    /// Pulls every owned game, then every unlocked achievement per game, into the local store.
    /// Reports progress as it goes and saves incrementally. Safe to cancel.
    /// </summary>
    public async Task<Result> BackfillAsync(IProgress<Progress>? progress = null, CancellationToken ct = default)
    {
        Log($"avvio — proxy='{Base}'");
        if (string.IsNullOrEmpty(Base) || Base.Contains("REPLACE-ME")) { Log("proxy non configurato -> NoProxy"); return Result.NoProxy; }

        ulong id = _config.ResolvedSteamId();
        Log($"steamid = {id}");
        if (id == 0) { Log("SteamID non rilevato -> NoSteamId"); return Result.NoSteamId; }

        string lang = SteamLang(_config.EffectiveLanguage);   // Steam wants "italian", not "it"

        OwnedResp? owned;
        try { owned = await GetJson<OwnedResp>($"{Base}/owned?steamid={id}", ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Log($"errore /owned: {ex.GetType().Name}: {ex.Message}"); return Result.Error; }

        if (owned == null || !owned.ok) { Log("risposta /owned non valida -> Error"); return Result.Error; }
        if (owned.@private) { Log("profilo privato -> ProfilePrivate"); return Result.ProfilePrivate; }

        var games = owned.games ?? new List<Game>();
        Log($"giochi posseduti: {games.Count}");
        int added = 0, doneCount = 0, total = games.Count;

        // Process games with bounded concurrency, collecting any that Steam rate-limited (the proxy
        // returns a non-OK response for those, never a false "0"). Then AUTOMATICALLY retry the failed
        // ones in further rounds with a short cooldown, so a single sync converges on its own — the
        // user never has to re-sync by hand.
        var pending = new List<Game>(games);
        const int MaxRounds = 6;
        for (int round = 0; round < MaxRounds && pending.Count > 0; round++)
        {
            ct.ThrowIfCancellationRequested();
            if (round > 0)
            {
                Log($"round {round}: riprovo {pending.Count} giochi rifiutati da Steam");
                await Task.Delay(TimeSpan.FromSeconds(4), ct);   // let Steam's rate limit cool down
            }
            var failed = new System.Collections.Concurrent.ConcurrentBag<Game>();
            using var sem = new SemaphoreSlim(4);                 // gentler than before: fewer rate-limits
            var tasks = new List<Task>();
            foreach (var g in pending)
            {
                ct.ThrowIfCancellationRequested();
                await sem.WaitAsync(ct);
                var game = g;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var a = await GetJson<AchResp>($"{Base}/ach?steamid={id}&appid={game.appid}&l={lang}", ct);
                        if (a == null || !a.ok) { failed.Add(game); return; }   // rate-limited → retry next round
                        long last = 0;
                        if (a.achievements != null)
                        {
                            foreach (var r in a.achievements)
                            {
                                if (string.IsNullOrEmpty(r.api)) continue;
                                if (r.unlock > last) last = r.unlock;
                                var rec = new AchievementRecord
                                {
                                    Appid = game.appid,
                                    Game = game.name ?? "",
                                    Api = r.api!,
                                    Name = string.IsNullOrEmpty(r.name) ? r.api! : r.name!,
                                    Desc = r.desc ?? "",
                                    Unlock = r.unlock,
                                    Percent = r.percent,
                                    Icon = r.icon ?? ""
                                };
                                if (_store.Upsert(rec)) System.Threading.Interlocked.Increment(ref added);
                            }
                        }
                        // Per-game completion for the "100% games" cards and the leaderboard score.
                        _games.Set(game.appid, game.name ?? "", a.total, a.unlocked, last);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { failed.Add(game); }   // transient error → retry next round
                    finally
                    {
                        int d = System.Threading.Interlocked.Increment(ref doneCount);
                        if (d % 25 == 0) { _store.Save(); _games.Save(); }
                        progress?.Report(new Progress(System.Math.Min(d, total), total, added));
                        sem.Release();
                    }
                }, ct));
            }
            await Task.WhenAll(tasks);
            _store.Save();
            _games.Save();
            pending = new List<Game>(failed);
        }
        if (pending.Count > 0) Log($"fine con {pending.Count} giochi ancora non risolti (ripresi alla prossima sync)");

        LastAdded = added;
        _config.HistoryImported = true;
        _config.LastSyncUtc = DateTime.UtcNow.ToString("o");
        _config.Save();
        Log($"fine — {games.Count} giochi, {added} obiettivi nuovi, totale in archivio {_store.Count}");
        return Result.Ok;
    }

    private static async Task<T?> GetJson<T>(string url, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return default;
        var s = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    // ---- proxy response shapes (lowercase to match the Worker's JSON) ----
    private class OwnedResp
    {
        public bool ok { get; set; }
        public bool @private { get; set; }
        public List<Game>? games { get; set; }
    }
    private class Game
    {
        public uint appid { get; set; }
        public string? name { get; set; }
        public int playtime { get; set; }
    }
    private class AchResp
    {
        public bool ok { get; set; }
        public uint appid { get; set; }
        public string? game { get; set; }
        public int total { get; set; }      // total achievements the game has
        public int unlocked { get; set; }   // how many the user has unlocked
        public List<Ach>? achievements { get; set; }
    }
    private class Ach
    {
        public string? api { get; set; }
        public string? name { get; set; }
        public string? desc { get; set; }
        public long unlock { get; set; }
        public double? percent { get; set; }
        public string? icon { get; set; }
    }
}
