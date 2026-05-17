using System.Diagnostics;
using System.IO;

namespace FocusGuard.Services;

/// <summary>
/// Polls <see cref="Process.GetProcessesByName"/> every <see cref="PollIntervalMs"/> milliseconds
/// and immediately kills any process whose name matches the blocked list.
///
/// Design notes:
/// - Pure user-mode polling: no kernel drivers, no DLL injection, no ETW hooks.
/// - One background task owns the loop; Apply() refreshes the executable snapshot.
/// - Each process is attempted once per poll cycle to avoid redundant Kill() calls.
/// </summary>
public sealed class ProcessWatchdogService : IProcessWatchdogService
{
    // Poll twice per second. This is still lightweight but catches game launchers quickly
    // enough that a restricted game usually closes before it becomes interactive.
    private const int PollIntervalMs = 500;

    private readonly ILoggingService _loggingService;

    // Immutable snapshot replaced atomically on every Apply() call.
    private volatile IReadOnlySet<string> _blockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string _lastAppliedKey = string.Empty;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public ProcessWatchdogService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public event EventHandler<ProcessKilledEventArgs>? ProcessKilled;

    public bool IsRunning => _loopTask is { IsCompleted: false };

    /// <inheritdoc />
    public void Apply(IReadOnlyCollection<string> executableNames)
    {
        // Normalise: strip paths, ensure .exe suffix, deduplicate.
        var normalised = executableNames
            .Select(NormaliseExecutableName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Atomically swap the blocked-name set that the loop reads.
        _blockedNames = normalised;

        if (normalised.Count == 0)
        {
            var shouldLogStop = IsRunning || !string.IsNullOrEmpty(_lastAppliedKey);
            _lastAppliedKey = string.Empty;
            // Nothing left to watch — stop the loop.
            StopLoop();
            if (shouldLogStop)
            {
                _loggingService.Info("Process watchdog stopped (no blocked executables).");
            }
            return;
        }

        var appliedKey = string.Join("|", normalised.Order(StringComparer.OrdinalIgnoreCase));
        var wasRunning = IsRunning;

        // Start the loop only if it is not already running.
        if (!wasRunning)
        {
            StartLoop();
            _loggingService.Info($"Process watchdog started. Blocking: {string.Join(", ", normalised)}.");
        }
        else if (!string.Equals(_lastAppliedKey, appliedKey, StringComparison.Ordinal))
        {
            _loggingService.Info($"Process watchdog updated. Blocking: {string.Join(", ", normalised)}.");
        }

        _lastAppliedKey = appliedKey;
    }

    /// <inheritdoc />
    public void Stop()
    {
        StopLoop();
        _blockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _loggingService.Info("Process watchdog stopped.");
    }

    public void Dispose()
    {
        StopLoop();
    }

    private void StartLoop()
    {
        StopLoop();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    private void StopLoop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                KillBlockedProcesses();

                await Task.Delay(PollIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path — nothing to log.
        }
        catch (Exception ex)
        {
            _loggingService.Error("Process watchdog loop stopped unexpectedly.", ex);
        }
    }

    private void KillBlockedProcesses()
    {
        // Capture the snapshot once per poll tick.
        var blocked = _blockedNames;
        if (blocked.Count == 0)
        {
            return;
        }

        // Track PIDs we already tried this tick to avoid double-kill on duplicates.
        HashSet<int>? killedThisTick = null;

        foreach (var name in blocked)
        {
            // GetProcessesByName expects the name without the .exe extension.
            var baseName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name[..^4]
                : name;

            Process[] found;
            try
            {
                found = Process.GetProcessesByName(baseName);
            }
            catch
            {
                // Process enumeration may fail under restricted accounts; skip silently.
                continue;
            }

            foreach (var process in found)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    killedThisTick ??= [];
                    if (!killedThisTick.Add(process.Id))
                    {
                        continue; // Already handled this PID this tick.
                    }

                    process.Kill(entireProcessTree: true);

                    _loggingService.Info(
                        $"Process watchdog killed restricted process: {process.ProcessName} (PID {process.Id}).");

                    ProcessKilled?.Invoke(this, new ProcessKilledEventArgs
                    {
                        ProcessName = process.ProcessName,
                        ProcessId = process.Id
                    });
                }
                catch (Exception ex)
                {
                    // A failed kill is non-fatal; log and continue.
                    _loggingService.Error(
                        $"Process watchdog could not kill {process.ProcessName} (PID {process.Id}).", ex);
                }
                finally
                {
                    try { process.Dispose(); } catch { /* ignore */ }
                }
            }
        }
    }

    private static string NormaliseExecutableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        name = Path.GetFileName(name.Trim());
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name}.exe";
    }
}
