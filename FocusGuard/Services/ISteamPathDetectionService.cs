using FocusGuard.Models;

namespace FocusGuard.Services;

public interface ISteamPathDetectionService
{
    Task<string> AutoDetectSteamPathAsync(CancellationToken cancellationToken = default);

    bool IsSteamRoot(string? path);

    Task<IReadOnlyList<string>> GetSteamLibraryPathsAsync(string steamPath, CancellationToken cancellationToken = default);

    Task<SteamGameState> GetGameStateAsync(string steamPath, int appId, CancellationToken cancellationToken = default);

    Task<bool> CancelInstallationAsync(string steamPath, int appId, CancellationToken cancellationToken = default);

    Task<bool> CancelInstallationAsync(
        string steamPath,
        int appId,
        bool removeManifest,
        CancellationToken cancellationToken = default);

    Task<SteamCleanupResult> CleanupInstallationAsync(
        string steamPath,
        int appId,
        SteamCleanupOptions options,
        CancellationToken cancellationToken = default);
}
