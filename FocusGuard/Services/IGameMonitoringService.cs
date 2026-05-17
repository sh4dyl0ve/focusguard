using FocusGuard.Models;

namespace FocusGuard.Services;

public interface IGameMonitoringService
{
    event EventHandler<IReadOnlyList<GameStatusUpdate>>? StatusesUpdated;

    bool IsRunning { get; }

    void Start(GameMonitoringConfiguration configuration);

    void Update(GameMonitoringConfiguration configuration);

    void Stop();

    Task<IReadOnlyList<GameStatusUpdate>> CheckGamesAsync(
        GameMonitoringConfiguration configuration,
        CancellationToken cancellationToken = default);
}
