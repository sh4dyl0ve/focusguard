namespace FocusGuard.Models;

public sealed class RestrictedGame
{
    public int AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GameStatus Status { get; set; } = GameStatus.NotInstalled;
    public string Details { get; set; } = "Not detected";
    public string? LibraryPath { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.Now;
}
