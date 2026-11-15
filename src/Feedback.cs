using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Sends the in-app «Send feedback» form to the Fanfara proxy (POST /feedback), which stores it in the
/// developer's Cloudflare D1 database. Along with the user's text it sends only Fanfara's version, the
/// UI language and the Windows version; the technical log is attached only if the user ticks the box,
/// and is scrubbed of personal data first. The SteamID travels only so the server can apply an
/// anonymous daily limit — the server stores a one-way hash, never the ID itself.
/// </summary>
public static class Feedback
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");

    public record Result(bool Ok, string? Error);

    public static async Task<Result> SendAsync(Config config, string kind, string text, string contact, bool attachLog, CancellationToken ct = default)
    {
        var baseUrl = (config.SyncProxyUrl ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("REPLACE-ME")) return new Result(false, "noproxy");
        text = (text ?? "").Trim();
        if (text.Length < 3) return new Result(false, "empty");

        var body = JsonSerializer.Serialize(new
        {
            kind = kind is "bug" or "idea" ? kind : "other",
            text = text.Length > 4000 ? text[..4000] : text,
            contact = (contact ?? "").Trim() is var c && c.Length > 200 ? c[..200] : (contact ?? "").Trim(),
            version = HomeData.AppVersion(),
            lang = config.EffectiveLanguage,
            os = RuntimeInformation.OSDescription,
            log = attachLog ? ReadLog() : "",
            steamid = config.ResolvedSteamId().ToString()
        });

        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(baseUrl + "/feedback", content, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var d = JsonDocument.Parse(raw);
                bool ok = d.RootElement.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
                string? err = d.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                return new Result(ok, ok ? null : (err ?? "server"));
            }
            catch { return new Result(false, resp.IsSuccessStatusCode ? "server" : "http"); }
        }
        catch { return new Result(false, "network"); }
    }

    /// <summary>Last lines of the log, with SteamIDs, the Windows user name and the profile path removed.</summary>
    public static string ReadLog()
    {
        try
        {
            if (!File.Exists(LogPath)) return "";
            string all;
            using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs)) all = sr.ReadToEnd();
            var lines = all.Replace("\r\n", "\n").Split('\n');
            var tail = string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 200)));
            return Scrub(tail, Environment.UserName,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        catch { return ""; }
    }

    /// <summary>Removes personal data from a text (public for testing).</summary>
    public static string Scrub(string text, string? userName, string? profilePath)
    {
        var s = text ?? "";
        if (!string.IsNullOrEmpty(profilePath)) s = s.Replace(profilePath, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        s = Regex.Replace(s, @"\b\d{17}\b", "[steamid]");
        if (!string.IsNullOrEmpty(userName) && userName.Length >= 3)
            s = Regex.Replace(s, Regex.Escape(userName), "[user]", RegexOptions.IgnoreCase);
        return s.Length > 25000 ? s[^25000..] : s;
    }
}
