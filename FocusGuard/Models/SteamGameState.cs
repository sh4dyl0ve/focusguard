namespace FocusGuard.Models;

public sealed class SteamGameState
{
    public int AppId { get; init; }
    public bool IsInstalled { get; init; }
    public bool IsInstalling { get; init; }
    public string? ManifestPath { get; init; }
    public string? DownloadingPath { get; init; }
    public string? LibraryPath { get; init; }

    public GameStatus ToDisplayStatus(bool protectionEnabled)
    {
        if (IsInstalling)
        {
            return GameStatus.Installing;
        }

        if (IsInstalled)
        {
            return protectionEnabled ? GameStatus.Restricted : GameStatus.Installed;
        }

        return GameStatus.NotInstalled;
    }
}
