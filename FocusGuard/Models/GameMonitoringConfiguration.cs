namespace FocusGuard.Models;

public sealed class GameMonitoringConfiguration
{
    public string SteamPath { get; init; } = string.Empty;
    public IReadOnlyList<int> AppIds { get; init; } = [];
    public bool ProtectionEnabled { get; init; }
    public int IntervalSeconds { get; init; } = 5;
}
