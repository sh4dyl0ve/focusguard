using FocusGuard.Models;

namespace FocusGuard.Services;

public sealed class GameMonitoringService : IGameMonitoringService, IDisposable
{
    private readonly ISteamPathDetectionService _steamDetectionService;
    private readonly ILoggingService _loggingService;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;
    private GameMonitoringConfiguration _configuration = new();

    public GameMonitoringService(
        ISteamPathDetectionService steamDetectionService,
        ILoggingService loggingService)
    {
        _steamDetectionService = steamDetectionService;
        _loggingService = loggingService;
    }

    public event EventHandler<IReadOnlyList<GameStatusUpdate>>? StatusesUpdated;

    public bool IsRunning => _monitoringTask is { IsCompleted: false };

    public void Start(GameMonitoringConfiguration configuration)
    {
        Stop();
        Update(configuration);

        _monitoringCts = new CancellationTokenSource();
        _monitoringTask = Task.Run(() => RunMonitoringLoopAsync(_monitoringCts.Token));
    }

    public void Update(GameMonitoringConfiguration configuration)
    {
        lock (_syncRoot)
        {
            _configuration = Normalize(configuration);
        }
    }

    public void Stop()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        _monitoringTask = null;
    }

    public async Task<IReadOnlyList<GameStatusUpdate>> CheckGamesAsync(
        GameMonitoringConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(configuration);
        if (string.IsNullOrWhiteSpace(normalized.SteamPath) || normalized.AppIds.Count == 0)
        {
            return [];
        }

        var updates = new List<GameStatusUpdate>(normalized.AppIds.Count);
        foreach (var appId in normalized.AppIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await _steamDetectionService.GetGameStateAsync(normalized.SteamPath, appId, cancellationToken);
            updates.Add(new GameStatusUpdate
            {
                AppId = appId,
                State = state,
                Status = state.ToDisplayStatus(normalized.ProtectionEnabled)
            });
        }

        return updates;
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task RunMonitoringLoopAsync(CancellationToken cancellationToken)
    {
        // Run once immediately, then poll every configured interval without blocking the WPF dispatcher.
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var configuration = GetConfigurationSnapshot();
                var updates = await CheckGamesAsync(configuration, cancellationToken);
                StatusesUpdated?.Invoke(this, updates);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.IntervalSeconds));
                if (!await timer.WaitForNextTickAsync(cancellationToken))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _loggingService.Error("Background game monitoring stopped unexpectedly", ex);
        }
    }

    private GameMonitoringConfiguration GetConfigurationSnapshot()
    {
        lock (_syncRoot)
        {
            return _configuration;
        }
    }

    private static GameMonitoringConfiguration Normalize(GameMonitoringConfiguration configuration)
    {
        return new GameMonitoringConfiguration
        {
            SteamPath = configuration.SteamPath,
            ProtectionEnabled = configuration.ProtectionEnabled,
            IntervalSeconds = Math.Clamp(configuration.IntervalSeconds, 2, 3600),
            AppIds = configuration.AppIds
                .Where(appId => appId > 0)
                .Distinct()
                .ToArray()
        };
    }
}
