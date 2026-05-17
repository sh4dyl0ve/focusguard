using System.Diagnostics;
using FocusGuard.Models;

namespace FocusGuard.Services;

public sealed class SteamProcessService : ISteamProcessService
{
    private static readonly string[] SteamProcessNames =
    [
        "steam",
        "steamwebhelper"
    ];

    public Task<SteamQuitResult> QuitSteamAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => QuitSteam(cancellationToken), cancellationToken);
    }

    private static SteamQuitResult QuitSteam(CancellationToken cancellationToken)
    {
        var processes = GetSteamProcesses();
        if (processes.Count == 0)
        {
            return new SteamQuitResult();
        }

        var closedCount = 0;
        var killedCount = 0;

        foreach (var process in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow())
                {
                    closedCount++;
                }
            }
            catch
            {
                // Steam helpers often have no visible window or exit while we are enumerating.
            }
        }

        WaitForExit(processes, TimeSpan.FromSeconds(4), cancellationToken);

        foreach (var process in processes.Where(process => !HasExited(process)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // This is an explicit user-controlled productivity setting, not stealth behavior.
                // It only targets Steam client processes by name and does not inspect memory or inject code.
                process.Kill(entireProcessTree: true);
                killedCount++;
            }
            catch
            {
                // A failed kill is non-fatal; the UI/log will explain that cleanup may be incomplete.
            }
        }

        WaitForExit(processes, TimeSpan.FromSeconds(2), cancellationToken);

        var result = new SteamQuitResult
        {
            FoundProcessCount = processes.Count,
            ClosedProcessCount = closedCount,
            ForceKilledProcessCount = killedCount
        };

        DisposeProcesses(processes);
        return result;
    }

    private static List<Process> GetSteamProcesses()
    {
        var processes = new List<Process>();
        foreach (var processName in SteamProcessNames)
        {
            try
            {
                processes.AddRange(Process.GetProcessesByName(processName));
            }
            catch
            {
                // Process enumeration can fail under restricted accounts; leave the app responsive.
            }
        }

        return processes
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static void WaitForExit(IEnumerable<Process> processes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        foreach (var process in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasExited(process))
            {
                continue;
            }

            try
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return;
                }

                process.WaitForExit((int)remaining.TotalMilliseconds);
            }
            catch
            {
                // Best effort: cleanup continues even if one process cannot be waited on.
            }
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static void DisposeProcesses(IEnumerable<Process> processes)
    {
        foreach (var process in processes)
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
        }
    }
}
