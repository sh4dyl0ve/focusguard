namespace FocusGuard.Models;

public sealed class FirewallRuleResult
{
    public bool Success { get; init; }
    public bool Changed { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}
