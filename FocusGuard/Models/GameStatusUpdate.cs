namespace FocusGuard.Models;

public sealed class GameStatusUpdate
{
    public required int AppId { get; init; }
    public required GameStatus Status { get; init; }
    public required SteamGameState State { get; init; }
}
