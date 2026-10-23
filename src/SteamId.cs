using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Fanfara;

/// <summary>
/// Finds the SteamID64 of the user currently (or most recently) signed into Steam on this PC,
/// read entirely from Steam's own local data — no login, no password, no network call.
/// Used to sync the achievement history through the Fanfara proxy.
/// </summary>
public static class SteamId
{
    // SteamID64 = this base + the 32-bit account id, for individual accounts.
    private const ulong Base = 76561197960265728UL;

    /// <summary>Best guess at the local user's SteamID64, or null if it can't be determined.</summary>
    public static ulong? Current()
    {
        // 1) The account signed in right now. ActiveUser is the 32-bit account id (0 if Steam isn't running).
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            if (k?.GetValue("ActiveUser") is int acc && acc > 0)
                return Base + (uint)acc;
        }
        catch { }

        // 2) Otherwise, the most recent account from loginusers.vdf.
        try { return FromLoginUsers(); }
        catch { return null; }
    }

    private static ulong? FromLoginUsers()
    {
        string? steam = null;
        try { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); steam = k?.GetValue("SteamPath") as string; }
        catch { }
        if (string.IsNullOrEmpty(steam)) return null;

        var vdf = Path.Combine(steam.Replace('/', '\\'), "config", "loginusers.vdf");
        if (!File.Exists(vdf)) return null;

        // loginusers.vdf is a simple keyed text file. Each block opens with a quoted 17-digit
        // SteamID64, and the signed-in account has  "MostRecent"  "1". Prefer MostRecent, else the first id.
        ulong firstId = 0, mostRecent = 0, current = 0;
        foreach (var raw in File.ReadLines(vdf))
        {
            var line = raw.Trim();
            var m = Regex.Match(line, "^\"(7656119\\d{10})\"$");
            if (m.Success)
            {
                current = ulong.Parse(m.Groups[1].Value);
                if (firstId == 0) firstId = current;
                continue;
            }
            if (current != 0)
            {
                var cond = Regex.Replace(line, "\\s", "");
                if (cond.Equals("\"MostRecent\"\"1\"", StringComparison.OrdinalIgnoreCase))
                    mostRecent = current;
            }
        }
        if (mostRecent != 0) return mostRecent;
        return firstId != 0 ? firstId : (ulong?)null;
    }
}
