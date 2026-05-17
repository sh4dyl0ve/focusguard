namespace FocusGuard.Config;

using FocusGuard.Models;

public sealed class AppSettings
{
    public string SteamPath { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool MonitoringEnabled { get; set; } = true;
    public bool FocusModeEnabled { get; set; } = true;
    public bool AutoCancelRestrictedInstallations { get; set; } = true;
    public bool ForcedSteamQuitEnabled { get; set; }
    public bool BlockSteamNetworkDuringRestrictedInstallations { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool BlockInstalledGameLaunchesEnabled { get; set; }
    public ProtectionMode ProtectionMode { get; set; } = ProtectionMode.Strict;
    public int CheckIntervalSeconds { get; set; } = 5;
    public string DisablePassword { get; set; } = string.Empty;
    public string DisablePasswordHash { get; set; } = string.Empty;
    public string DisablePasswordSalt { get; set; } = string.Empty;
    public int DisablePasswordIterations { get; set; } = 120_000;
    public List<GameRestrictionSetting> RestrictedGames { get; set; } =
    [
        new GameRestrictionSetting
        {
            AppId = 570,
            Name = "Dota 2",
            ExecutableName = "dota2.exe"
        }
    ];
}

public sealed class GameRestrictionSetting
{
    public int AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
}
