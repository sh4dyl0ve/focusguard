namespace FocusGuard.Services;

public interface INotificationService
{
    void ShowRestrictedGameDetected(string gameName, int appId);

    void NotifySteamDetected(string steamPath);

    void NotifyRestrictedGameInstalling(string gameName, int appId);

    void NotifyRestrictedGameLaunched(string gameName, int appId);

    void NotifyFocusSessionStarted();

    void NotifyFocusSessionEnded();

    bool ConfirmCancelInstallation(string gameName, int appId);

    string? PromptForDisablePassword();

    void ShowInfo(string message);

    void ShowError(string message);
}
