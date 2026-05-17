namespace FocusGuard.Models;

public sealed class SteamQuitResult
{
    public int FoundProcessCount { get; init; }
    public int ClosedProcessCount { get; init; }
    public int ForceKilledProcessCount { get; init; }

    public bool WasRunning => FoundProcessCount > 0;
    public bool ClosedAny => ClosedProcessCount > 0 || ForceKilledProcessCount > 0;
}
