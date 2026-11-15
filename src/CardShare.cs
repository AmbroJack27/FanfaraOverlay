using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Posts a Collection card (a PNG rendered by the app) to the Fanfara Discord showcase channel
/// (#bacheca-trofei) through the proxy (POST /share). The proxy checks on Steam that the achievement
/// is really unlocked on the user's profile before posting, so nobody can show off a fake trophy, and
/// it allows each card only once and a few cards a day.
/// </summary>
public static class CardShare
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(40) };

    public record Card(uint Appid, string Api, string Name, string Game, double? Pct, long Unlock, string Side);

    public static async Task<Feedback.Result> PostAsync(Config config, byte[] png, Card card, CancellationToken ct = default)
    {
        var baseUrl = (config.SyncProxyUrl ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("REPLACE-ME")) return new Feedback.Result(false, "noproxy");
        ulong id = config.ResolvedSteamId();
        if (id == 0) return new Feedback.Result(false, "nosteamid");
        if (card.Appid == 0 || string.IsNullOrEmpty(card.Api) || card.Api.StartsWith("live:")) return new Feedback.Result(false, "notyours");

        var meta = JsonSerializer.Serialize(new
        {
            steamid = id.ToString(), appid = card.Appid, api = card.Api, name = card.Name, game = card.Game,
            p = card.Pct, unlock = card.Unlock, side = card.Side, lang = config.EffectiveLanguage, version = HomeData.AppVersion()
        });
        try
        {
            // Names are quoted explicitly: .NET writes name=meta without quotes, which strict multipart
            // parsers (like the one in Cloudflare Workers) may reject.
            using var form = new MultipartFormDataContent();
            var metaPart = new StringContent(meta, Encoding.UTF8);
            metaPart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"meta\"" };
            form.Add(metaPart);
            var img = new ByteArrayContent(png);
            img.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            img.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"image\"", FileName = "\"carta.png\"" };
            form.Add(img);
            using var resp = await Http.PostAsync(baseUrl + "/share", form, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var d = JsonDocument.Parse(raw);
                bool ok = d.RootElement.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
                string? err = d.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                return new Feedback.Result(ok, ok ? null : (err ?? "server"));
            }
            catch { return new Feedback.Result(false, "server"); }
        }
        catch { return new Feedback.Result(false, "network"); }
    }
}
