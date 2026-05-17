using FocusGuard.Models;

namespace FocusGuard.Services;

public interface ISteamProcessService
{
    Task<SteamQuitResult> QuitSteamAsync(CancellationToken cancellationToken = default);
}
