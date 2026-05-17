namespace FocusGuard.Services;

/// <summary>
/// Watches for restricted process names and kills them immediately when they appear.
/// Works as a second line of defence alongside the Windows launch policy (DisallowRun registry).
/// </summary>
public interface IProcessWatchdogService : IDisposable
{
    /// <summary>Raised on the thread-pool each time a restricted process is killed.</summary>
    event EventHandler<ProcessKilledEventArgs>? ProcessKilled;

    bool IsRunning { get; }

    /// <summary>
    /// Start (or restart) the watchdog with the given list of executable names to block.
    /// Passing an empty collection stops the watchdog.
    /// </summary>
    void Apply(IReadOnlyCollection<string> executableNames);

    /// <summary>Stop the watchdog and release resources.</summary>
    void Stop();
}

public sealed class ProcessKilledEventArgs : EventArgs
{
    public required string ProcessName { get; init; }
    public required int ProcessId { get; init; }
}
