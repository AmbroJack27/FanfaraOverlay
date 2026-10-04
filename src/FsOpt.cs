using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Fanfara;

/// <summary>
/// Turns off Windows "fullscreen optimizations" for a specific game .exe, per-user and
/// WITHOUT administrator rights. This is the same thing as ticking
/// "Disable fullscreen optimizations" on the exe's Compatibility tab: it forces the game's
/// borderless/fullscreen presentation to stay composited by the desktop, so a topmost
/// overlay like Fanfara can actually draw on top of it.
///
/// The setting lives in HKCU\...\AppCompatFlags\Layers as a space-separated string whose
/// first token is "~" (per-user layer marker) followed by flag names. We only add/remove the
/// DISABLEDXMAXIMIZEDWINDOWEDMODE flag and leave any other flags the user may have set.
/// Takes effect the next time the game launches.
/// </summary>
public static class FsOpt
{
    private const string LayersKey =
        @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string Flag = "DISABLEDXMAXIMIZEDWINDOWEDMODE";

    /// <summary>Returns true only if the flag was newly added (false if already set, or on error).</summary>
    public static bool Apply(string exePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exePath)) return false;
            using var key = Registry.CurrentUser.CreateSubKey(LayersKey, writable: true);
            if (key == null) return false;

            var cur = key.GetValue(exePath) as string ?? "";
            var tokens = new List<string>(cur.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

            bool alreadyHad = tokens.Contains(Flag);
            if (!tokens.Contains("~")) tokens.Insert(0, "~");
            if (!alreadyHad) tokens.Add(Flag);

            key.SetValue(exePath, string.Join(" ", tokens), RegistryValueKind.String);
            return !alreadyHad;
        }
        catch { return false; }
    }

    /// <summary>Removes only our flag; deletes the value if nothing meaningful remains.</summary>
    public static void Remove(string exePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exePath)) return;
            using var key = Registry.CurrentUser.OpenSubKey(LayersKey, writable: true);
            if (key == null) return;
            if (key.GetValue(exePath) is not string cur) return;

            var tokens = new List<string>(cur.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            tokens.RemoveAll(t => t == Flag);

            if (tokens.Count == 0 || (tokens.Count == 1 && tokens[0] == "~"))
                key.DeleteValue(exePath, throwOnMissingValue: false);
            else
                key.SetValue(exePath, string.Join(" ", tokens), RegistryValueKind.String);
        }
        catch { }
    }
}
