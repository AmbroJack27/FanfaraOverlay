using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fanfara;

/// <summary>
/// Checks GitHub Releases for a newer version and, on the user's confirmation,
/// downloads the installer (FanfaraSetup.exe) and launches it.
/// </summary>
public static class Updater
{
    private const string Owner = "AmbroJack27";
    private const string Repo = "FanfaraOverlay";
    private const string AssetName = "FanfaraSetup.exe";
    // Optional checksum asset published next to the installer (see release workflow). When present
    // we verify the download against it before running it; when absent we fall back to running the
    // HTTPS-downloaded installer as before (older releases have no checksum).
    private const string HashAssetName = "FanfaraSetup.exe.sha256";

    public record UpdateInfo(string Version, string Tag, string DownloadUrl, string? Sha256Url, string Body = "");

    /// <summary>Current app version as Major.Minor.Build (revision ignored).</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
        }
    }

    /// <summary>Returns info about a newer release, or null (no update / offline / error).</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var json = await http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var verStr = tag.TrimStart('v', 'V');
            if (!Version.TryParse(Normalize(verStr), out var latest)) return null;
            if (latest <= CurrentVersion) return null;

            string? dl = null;
            string? hashUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString();
                    if (string.Equals(n, AssetName, StringComparison.OrdinalIgnoreCase))
                        dl = a.GetProperty("browser_download_url").GetString();
                    else if (string.Equals(n, HashAssetName, StringComparison.OrdinalIgnoreCase))
                        hashUrl = a.GetProperty("browser_download_url").GetString();
                }
            }
            if (string.IsNullOrEmpty(dl)) return null;

            string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            return new UpdateInfo(verStr, tag, dl!, hashUrl, body);
        }
        catch { return null; }
    }

    /// <summary>
    /// Downloads the installer to a temp file, verifies its SHA-256 against the release's
    /// checksum asset (when one is published), and starts it. Returns true on success.
    /// If the checksum is present and does NOT match, the installer is deleted and NOT run.
    /// </summary>
    public static async Task<bool> DownloadAndRunAsync(UpdateInfo info)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");

            var bytes = await http.GetByteArrayAsync(info.DownloadUrl);

            // Integrity check: if the release ships a .sha256 asset, the download must match it.
            if (!string.IsNullOrEmpty(info.Sha256Url))
            {
                string? expected = null;
                try { expected = ParseHash(await http.GetStringAsync(info.Sha256Url)); } catch { }
                if (!string.IsNullOrEmpty(expected))
                {
                    var actual = Convert.ToHexString(SHA256.HashData(bytes));
                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                        return false;   // tampered / corrupted download — refuse to run it
                }
            }

            var path = Path.Combine(Path.GetTempPath(), $"FanfaraSetup-{info.Version}.exe");
            await File.WriteAllBytesAsync(path, bytes);

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    // ================= 0.6.0: one-click update =================

    /// <summary>Downloads the installer and checks its SHA-256. Returns the local path, or null (failed / not genuine).</summary>
    public static async Task<string?> DownloadAsync(UpdateInfo info, IProgress<int>? progress = null)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");
            using var resp = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            long len = resp.Content.Headers.ContentLength ?? 0;
            using var src = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buf = new byte[81920]; int n; long got = 0; int last = -1;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                ms.Write(buf, 0, n); got += n;
                if (len > 0) { int pc = (int)(got * 100 / len); if (pc != last) { last = pc; progress?.Report(pc); } }
            }
            var bytes = ms.ToArray();
            if (!string.IsNullOrEmpty(info.Sha256Url))
            {
                string? expected = null;
                try { expected = ParseHash(await http.GetStringAsync(info.Sha256Url)); } catch { }
                if (!string.IsNullOrEmpty(expected) &&
                    !string.Equals(expected, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase))
                    return null;   // tampered / corrupted: never run it
            }
            var path = Path.Combine(Path.GetTempPath(), $"FanfaraSetup-{info.Version}.exe");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch { return null; }
    }

    /// <summary>
    /// Starts the installer silently a couple of seconds after Fanfara has closed (so nothing is in use),
    /// with no windows; the installer starts Fanfara again when it's done (see Fanfara.iss).
    /// </summary>
    public static bool RunSilentAfterExit(string installerPath)
    {
        try
        {
            var args = $"/c ping -n 4 127.0.0.1 >nul & start \"\" \"{installerPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-";
            Process.Start(new ProcessStartInfo("cmd.exe", args) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            return true;
        }
        catch { return false; }
    }

    /// <summary>The release notes of one version (for the «What's new» window after an update), or "".</summary>
    public static async Task<string> NotesAsync(string version)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Fanfara-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var json = await http.GetStringAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/tags/v{version}");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        }
        catch { return ""; }
    }

    /// <summary>The part of the release notes in the user's language: «## 🇮🇹 Novità» for Italian, otherwise «## 🇬🇧 What's new».</summary>
    public static string Section(string body, string lang)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        string Find(string marker)
        {
            var lines = body.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("## ") && l.Contains(marker, StringComparison.OrdinalIgnoreCase));
            if (start < 0) return "";
            int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("## "));
            if (end < 0) end = lines.Length;
            return string.Join("\n", lines[(start + 1)..end]).Trim();
        }
        var it = lang == "it" ? Find("Novità") : "";
        return it.Length > 0 ? it : (Find("What's new") is { Length: > 0 } en ? en : body.Trim());
    }

    // Pulls the 64-hex-char SHA-256 out of a checksum file. Accepts a bare hash or the
    // "sha256sum" format ("&lt;hash&gt;  FanfaraSetup.exe").
    private static string? ParseHash(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var token = text.Trim().Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in token)
            if (t.Length == 64 && IsHex(t)) return t;
        return null;
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    // Make "0.1.4" / "0.1" parse to a 3-part Version.
    private static string Normalize(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch { 1 => v + ".0.0", 2 => v + ".0", _ => v };
    }
}
