using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Talks to the Fanfara proxy's leaderboard endpoints (/lb/*). All networking for the online
/// 100%-completion ranking lives here; the Classifica web screen is pure presentation and never
/// calls the network itself. Returns the proxy's raw JSON so the web layer can render it directly.
/// </summary>
public class Leaderboard
{
    private readonly Config _config;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };

    public Leaderboard(Config config) { _config = config; }

    private string Base => (_config.SyncProxyUrl ?? "").TrimEnd('/');
    private ulong Id => _config.ResolvedSteamId();
    public bool Ready => !string.IsNullOrEmpty(Base) && !Base.Contains("REPLACE-ME") && Id != 0;

    private string Lang => _config.EffectiveLanguage;

    /// <summary>Opt in to the public world board, submitting the locally-computed score. Returns raw JSON or null.</summary>
    public Task<string?> JoinAsync(int perfect, int total, CancellationToken ct = default)
        => Get($"{Base}/lb/join?steamid={Id}&l={Lang}&perfect={perfect}&total={total}", ct);

    /// <summary>Leave the public world board (deletes the row server-side). Returns raw JSON or null.</summary>
    public Task<string?> LeaveAsync(CancellationToken ct = default)
        => Get($"{Base}/lb/leave?steamid={Id}", ct);

    /// <summary>Refresh the member's own provisional score (no effect if not a member). Returns raw JSON or null.</summary>
    public Task<string?> SubmitAsync(int perfect, int total, CancellationToken ct = default)
        => Get($"{Base}/lb/submit?steamid={Id}&perfect={perfect}&total={total}", ct);

    /// <summary>
    /// Refresh my score AND tell the proxy which games are at 100% (0.6.0+), so my Steam friends' Fanfara
    /// can announce a newly completed game. Falls back to the plain refresh if the list can't be sent.
    /// </summary>
    public async Task<string?> SubmitAsync(int perfect, int total, System.Collections.Generic.List<GameRecord> perfectGames, CancellationToken ct = default)
    {
        try
        {
            var games = new System.Collections.Generic.List<object[]>();
            foreach (var g in perfectGames) games.Add(new object[] { g.Appid, g.Name ?? "", g.LastUnlock });
            var body = System.Text.Json.JsonSerializer.Serialize(new { games });
            using var resp = await Http.PostAsync($"{Base}/lb/submit?steamid={Id}&perfect={perfect}&total={total}",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
            if (resp.IsSuccessStatusCode) return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        return await SubmitAsync(perfect, total, ct);
    }

    /// <summary>The public world ranking (+ the user's own rank). Returns raw JSON or null.</summary>
    public Task<string?> WorldAsync(CancellationToken ct = default)
        => Get($"{Base}/lb/world?steamid={Id}", ct);

    /// <summary>The user's Steam friends ranked (model B: computed from public data). Returns raw JSON or null.</summary>
    public Task<string?> FriendsAsync(CancellationToken ct = default)
        => Get($"{Base}/lb/friends?steamid={Id}&l={Lang}", ct);

    /// <summary>For one game: which friends who use Fanfara unlocked which achievements, and when. Raw JSON or null.</summary>
    public Task<string?> FriendsAchAsync(uint appid, CancellationToken ct = default)
        => Get($"{Base}/lb/friends-ach?steamid={Id}&appid={appid}", ct);

    private static async Task<string?> Get(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await Http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch { return null; }
    }
}
