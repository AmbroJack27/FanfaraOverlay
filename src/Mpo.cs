using System;
using System.Diagnostics;

namespace Fanfara;

/// <summary>
/// Forces DWM "Composed Flip" by disabling Multi-Plane Overlay (MPO) machine-wide, so a topmost
/// overlay like Fanfara can draw over games that use Frame Generation or exclusive fullscreen
/// (which otherwise use Independent Flip and bypass the desktop compositor).
///
/// This writes HKLM\...\Dwm\OverlayTestMode = 5, which needs administrator rights, so the change
/// is applied by launching reg.exe elevated (one UAC prompt). It is machine-wide and reversible,
/// and takes effect after a reboot. This is a Windows display setting — NOT game injection — so it
/// does not interact with anti-cheat.
/// </summary>
public static class Mpo
{
    private const string Key = @"HKLM\SOFTWARE\Microsoft\Windows\Dwm";

    /// <summary>
    /// Enable (disabled=true) or revert (disabled=false) the MPO-off setting, elevated.
    /// Returns true on success, false if the user cancelled UAC or the command failed.
    /// </summary>
    public static bool SetDisabled(bool disabled)
    {
        try
        {
            string args = disabled
                ? $"add \"{Key}\" /v OverlayTestMode /t REG_DWORD /d 5 /f"
                : $"delete \"{Key}\" /v OverlayTestMode /f";

            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = args,
                UseShellExecute = true,   // required for the "runas" (UAC) verb
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit();

            // "add" must succeed (exit 0). For "delete", a missing value (exit 1) still means
            // the setting is effectively off, so treat any clean run as success.
            return disabled ? p.ExitCode == 0 : true;
        }
        catch
        {
            // Win32Exception here means the user cancelled the UAC prompt.
            return false;
        }
    }
}
