using FocusGuard.Models;

namespace FocusGuard.Services;

public interface IWindowsFirewallService
{
    Task<FirewallRuleResult> BlockSteamOutboundAsync(
        string steamExecutablePath,
        CancellationToken cancellationToken = default);

    Task<FirewallRuleResult> RemoveSteamOutboundBlockAsync(CancellationToken cancellationToken = default);
}
