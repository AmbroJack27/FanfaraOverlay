using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Fanfara;

/// <summary>
/// Supervises Steam achievement watching WITHOUT loading Steam into this process.
/// It watches the registry for the running game and, for each game, launches a short-lived
/// child process (this same exe with "--steam-worker &lt;appid&gt;") that talks to Steam and
/// prints unlocks. When the game closes the child is stopped/exits and the Steam session is
/// released — so the main app never crashes and Steam no longer shows the game as running.
/// </summary>
public class SteamWatcher
{
    public event Action<string, double>? AchievementUnlocked;

    private CancellationTokenSource? _cts;
    private readonly int _pollMs;
    private uint _currentApp;
    private uint _lastClosedApp;   // don't immediately respawn the game that just closed
    private Process? _worker;

    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");

    public SteamWatcher(int pollMs = 1000) => _pollMs = pollMs;

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {msg}{Environment.NewLine}"); } catch { }
    }

    public void Start()
    {
        try { File.WriteAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  Fanfara watcher avviato{Environment.NewLine}"); } catch { }
        _cts = new CancellationTokenSource();
        Task.Run(() => Loop(_cts.Token));
    }

    public void Stop() { _cts?.Cancel(); StopWorker(); }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                uint app = ReadRunningAppId();

                // The worker exits on its own when the real game closes.
                if (_worker != null && _worker.HasExited)
                {
                    Log($"operaio terminato (era {_currentApp})");
                    _worker.Dispose(); _worker = null;
                    if (_currentApp != 0) { _lastClosedApp = _currentApp; _currentApp = 0; }
                }

                if (app == 0)
                {
                    if (_currentApp != 0) { Log($"RunningAppID 0 (era {_currentApp})"); StopWorker(); _currentApp = 0; }
                    _lastClosedApp = 0;   // registry cleared: the game may be launched again now
                }
                else if (app != _currentApp && app != _lastClosedApp)
                {
                    Log($"RunningAppID -> {app}");
                    StopWorker();
                    StartWorker(app);
                    _currentApp = app;
                }
            }
            catch (Exception ex) { Log($"errore loop: {ex.GetType().Name}: {ex.Message}"); }

            try { await Task.Delay(_pollMs, ct); } catch { }
        }
    }

    private void StartWorker(uint app)
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) { Log("impossibile determinare il percorso dell'exe"); return; }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--steam-worker {app}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            _worker = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _worker.OutputDataReceived += (_, e) => { if (e.Data != null) HandleLine(e.Data); };
            _worker.Start();
            _worker.BeginOutputReadLine();
            Log($"operaio Steam avviato per {app} (pid {_worker.Id})");
        }
        catch (Exception ex) { Log($"avvio operaio fallito: {ex.Message}"); _worker = null; }
    }

    private void StopWorker()
    {
        var w = _worker;
        _worker = null;
        if (w == null) return;
        try { if (!w.HasExited) w.Kill(entireProcessTree: true); } catch { }
        try { w.Dispose(); } catch { }
    }

    private void HandleLine(string line)
    {
        // Expected: UNLOCK \t name \t pct
        var parts = line.Split('\t');
        if (parts.Length >= 3 && parts[0] == "UNLOCK")
        {
            string name = parts[1];
            double pct = double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 50.0;
            Log($"SBLOCCO nome='{name}' global={pct:0.0}%");
            try { AchievementUnlocked?.Invoke(name, pct); } catch { }
        }
    }

    private static uint ReadRunningAppId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var v = key?.GetValue("RunningAppID");
            return v is int i && i > 0 ? (uint)i : 0u;
        }
        catch { return 0u; }
    }
}
