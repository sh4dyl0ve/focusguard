using FocusGuard.Config;

namespace FocusGuard.Services;

public interface IAppSettingsService
{
    string SettingsPath { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
