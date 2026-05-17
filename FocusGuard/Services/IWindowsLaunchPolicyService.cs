namespace FocusGuard.Services;

public interface IWindowsLaunchPolicyService
{
    Task ApplyBlockedExecutablesAsync(
        IReadOnlyCollection<string> executableNames,
        CancellationToken cancellationToken = default);

    Task ClearFocusGuardEntriesAsync(CancellationToken cancellationToken = default);
}
