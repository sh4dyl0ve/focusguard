namespace FocusGuard.Models;

public sealed class SteamCleanupOptions
{
    public bool RemoveManifest { get; init; }
    public int VerificationAttempts { get; init; } = 3;
    public int VerificationDelayMilliseconds { get; init; } = 450;
}
