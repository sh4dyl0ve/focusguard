namespace FocusGuard.Models;

public sealed class ActivityLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
}
