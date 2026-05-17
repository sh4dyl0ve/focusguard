using FocusGuard.Models;

namespace FocusGuard.Services;

public interface ILoggingService
{
    event EventHandler<ActivityLogEntry>? EntryWritten;

    void Info(string message);

    void Error(string message, Exception? exception = null);
}
