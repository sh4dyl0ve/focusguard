namespace FocusGuard.Models;

public sealed class SteamCleanupResult
{
    public int AppId { get; init; }
    public bool RemovedDownloadingFolder { get; init; }
    public bool RemovedManifest { get; init; }
    public bool Verified { get; init; }
    public int Attempts { get; init; }
    public string? RemainingDownloadingPath { get; init; }
    public string? RemainingManifestPath { get; init; }
    public string? Message { get; init; }

    public bool RemovedAny => RemovedDownloadingFolder || RemovedManifest;
}
