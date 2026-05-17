using FocusGuard.Models;

namespace FocusGuard.Services;

public sealed class ActivityLoggingService : ILoggingService
{
    public event EventHandler<ActivityLogEntry>? EntryWritten;

    public void Info(string message)
    {
        Write(message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var finalMessage = exception is null ? message : $"{message}: {exception.Message}";
        Write(finalMessage);
    }

    private void Write(string message)
    {
        EntryWritten?.Invoke(this, new ActivityLogEntry
        {
            Timestamp = DateTime.Now,
            Message = message
        });
    }
}
