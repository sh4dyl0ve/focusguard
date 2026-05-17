using System.IO;
using System.Text.Json;
using FocusGuard.Config;
using FocusGuard.Models;

namespace FocusGuard.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly ILoggingService _loggingService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettingsService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusGuard");

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(SettingsDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Normalize(settings);
            return settings;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            BackupCorruptedSettings(ex);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(SettingsDirectory);
        Normalize(settings);

        var tempPath = Path.Combine(SettingsDirectory, "settings.tmp");
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        File.Move(tempPath, SettingsPath, overwrite: true);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.SteamPath ??= string.Empty;
        settings.Language = NormalizeLanguage(settings.Language);
        if (!Enum.IsDefined(settings.ProtectionMode))
        {
            settings.ProtectionMode = settings.ForcedSteamQuitEnabled
                ? ProtectionMode.Lockdown
                : settings.AutoCancelRestrictedInstallations ? ProtectionMode.Strict : ProtectionMode.Soft;
        }

        settings.DisablePassword ??= string.Empty;
        settings.DisablePasswordHash ??= string.Empty;
        settings.DisablePasswordSalt ??= string.Empty;
        settings.DisablePasswordIterations = settings.DisablePasswordIterations > 0
            ? settings.DisablePasswordIterations
            : 120_000;
        settings.RestrictedGames ??= [];
        foreach (var game in settings.RestrictedGames)
        {
            game.Name ??= string.Empty;
            game.ExecutableName ??= string.Empty;
        }

        settings.CheckIntervalSeconds = Math.Clamp(settings.CheckIntervalSeconds, 2, 3600);
        settings.RestrictedGames = settings.RestrictedGames
            .Where(game => game.AppId > 0)
            .GroupBy(game => game.AppId)
            .Select(group => new GameRestrictionSetting
            {
                AppId = group.Key,
                Name = string.IsNullOrWhiteSpace(group.First().Name)
                    ? $"Steam App {group.Key}"
                    : group.First().Name.Trim(),
                ExecutableName = NormalizeExecutableName(group.Key, group.First().ExecutableName)
            })
            .OrderBy(game => game.Name)
            .ToList();

        if (settings.RestrictedGames.Count == 0)
        {
            settings.RestrictedGames.Add(new GameRestrictionSetting
            {
                AppId = 570,
                Name = "Dota 2",
                ExecutableName = "dota2.exe"
            });
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        return language switch
        {
            "ru" => "ru",
            "zh-Hans" or "zh" or "zh-CN" => "zh-Hans",
            _ => "en"
        };
    }

    private static string NormalizeExecutableName(int appId, string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName) && appId == 570)
        {
            return "dota2.exe";
        }

        executableName = executableName.Trim();
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return string.Empty;
        }

        return executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executableName
            : $"{executableName}.exe";
    }

    private void BackupCorruptedSettings(Exception exception)
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var backupPath = Path.Combine(
                SettingsDirectory,
                $"settings.corrupt.{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Copy(SettingsPath, backupPath, overwrite: true);
            _loggingService.Error($"Settings file was corrupt and has been backed up to {backupPath}", exception);
        }
        catch (Exception backupException)
        {
            _loggingService.Error("Settings file was corrupt and backup failed", backupException);
        }
    }
}
