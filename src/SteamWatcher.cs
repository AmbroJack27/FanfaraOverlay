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
    // name, globalPercent, description, appid, apiName (appid+api feed the History/Collection store)
    public event Action<string, double, string, uint, string>? AchievementUnlocked;
    /// <summary>Raised once when a game's overlay fix was newly applied (user should restart that game).</summary>
    public event Action? GameOptimized;

    private CancellationTokenSource? _cts;
    private readonly int _pollMs;
    private readonly Config _config;
    private uint _currentApp;
    private uint _lastClosedApp;   // don't immediately respawn the game that just closed
    private Process? _worker;
    // If the worker dies unexpectedly (crash / Steam init hiccup) WHILE the game is still running,
    // we respawn it instead of giving up and missing every later unlock. Bounded so a genuinely
    // broken worker (e.g. Steam not running) doesn't spin forever. Reset once the worker proves
    // healthy (it emits a line) or when a new game starts.
    private volatile int _workerRetries;
    private const int MaxWorkerRetries = 3;

    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "fanfara-log.txt");

    public SteamWatcher(Config config) { _config = config; _pollMs = config.PollMs > 0 ? config.PollMs : 1000; }

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

                // The worker exits on its own (exit code 0) when it detects the real game closed.
                // Any other exit while the game is still running is unexpected (a crash or a Steam
                // init hiccup): respawn the worker a few times rather than silently stop notifying.
                if (_worker != null && _worker.HasExited)
                {
                    int code = SafeExitCode(_worker);
                    _worker.Dispose(); _worker = null;

                    bool gameStillRunning = _currentApp != 0 && app == _currentApp;
                    if (code != 0 && gameStillRunning && _workerRetries < MaxWorkerRetries)
                    {
                        _workerRetries++;
                        Log($"operaio uscito inatteso (code {code}), gioco {_currentApp} ancora attivo -> riavvio {_workerRetries}/{MaxWorkerRetries}");
                        StartWorker(_currentApp);   // keep _currentApp; next poll checks health again
                    }
                    else
                    {
                        Log($"operaio terminato (era {_currentApp}, code {code})");
                        if (_currentApp != 0) { _lastClosedApp = _currentApp; _currentApp = 0; }
                        _workerRetries = 0;
                    }
                }

                if (app == 0)
                {
                    if (_currentApp != 0) { Log($"RunningAppID 0 (era {_currentApp})"); StopWorker(); _currentApp = 0; }
                    _lastClosedApp = 0;   // registry cleared: the game may be launched again now
                    _workerRetries = 0;
                }
                else if (app != _currentApp && app != _lastClosedApp)
                {
                    Log($"RunningAppID -> {app}");
                    StopWorker();
                    _workerRetries = 0;
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

    private static int SafeExitCode(Process p)
    {
        try { return p.ExitCode; } catch { return -1; }
    }

    private void HandleLine(string line)
    {
        // The worker produced output, so it initialized fine — clear the unexpected-exit retry count.
        _workerRetries = 0;

        var parts = line.Split('\t');

        // UNLOCK \t name \t pct \t desc \t appid \t api
        if (parts.Length >= 3 && parts[0] == "UNLOCK")
        {
            string name = parts[1];
            double pct = double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 50.0;
            string desc = parts.Length >= 4 ? parts[3] : "";
            uint appid = parts.Length >= 5 && uint.TryParse(parts[4], out var ai) ? ai : 0u;
            string api = parts.Length >= 6 ? parts[5] : "";
            Log($"SBLOCCO nome='{name}' global={pct:0.0}% app={appid}");
            try { AchievementUnlocked?.Invoke(name, pct, desc, appid, api); } catch { }
            return;
        }

        // GAMEEXE \t <full path> — make the overlay able to draw over this game.
        if (parts.Length >= 2 && parts[0] == "GAMEEXE")
        {
            string exe = parts[1];
            if (!_config.GameOverlayFix) return;
            try
            {
                bool added = FsOpt.Apply(exe);
                if (_config.AddOptimizedGame(exe))   // thread-safe add + dedupe
                    _config.Save();
                if (added)
                {
                    Log($"ottimizzazione overlay applicata: {exe}");
                    try { GameOptimized?.Invoke(); } catch { }
                }
            }
            catch (Exception ex) { Log($"fix overlay fallito: {ex.Message}"); }
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
