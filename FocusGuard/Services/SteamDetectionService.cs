using System.IO;
using System.Text.RegularExpressions;
using FocusGuard.Models;
using Microsoft.Win32;

namespace FocusGuard.Services;

public sealed partial class SteamDetectionService : ISteamPathDetectionService
{
    private static readonly string[] StandardSteamPaths =
    [
        @"C:\Program Files (x86)\Steam",
        @"C:\Program Files\Steam",
        @"D:\Steam",
        @"E:\Steam"
    ];

    public Task<string> AutoDetectSteamPathAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return AutoDetectSteamPath();
        }, cancellationToken);
    }

    private string AutoDetectSteamPath()
    {
        foreach (var path in EnumerateSteamCandidates()
                     .Select(NormalizeCandidatePath)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IsSteamRoot(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return string.Empty;
    }

    public bool IsSteamRoot(string? path)
    {
        var normalizedPath = NormalizeCandidatePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath) || !Directory.Exists(normalizedPath))
        {
            return false;
        }

        return File.Exists(Path.Combine(normalizedPath, "steam.exe"))
            && Directory.Exists(Path.Combine(normalizedPath, "steamapps"));
    }

    public Task<IReadOnlyList<string>> GetSteamLibraryPathsAsync(string steamPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return GetSteamLibraryPaths(steamPath);
        }, cancellationToken);
    }

    private IReadOnlyList<string> GetSteamLibraryPaths(string steamPath)
    {
        if (!IsSteamRoot(steamPath))
        {
            return [];
        }

        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            libraries.Add(Path.GetFullPath(steamPath));
        }
        catch
        {
            return [];
        }

        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersPath))
        {
            foreach (var libraryPath in ReadLibraryFolders(libraryFoldersPath))
            {
                if (Directory.Exists(Path.Combine(libraryPath, "steamapps")))
                {
                    libraries.Add(Path.GetFullPath(libraryPath));
                }
            }
        }

        return libraries.ToList();
    }

    public Task<SteamGameState> GetGameStateAsync(string steamPath, int appId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return GetGameState(steamPath, appId);
        }, cancellationToken);
    }

    private SteamGameState GetGameState(string steamPath, int appId)
    {
        foreach (var libraryPath in GetSteamLibraryPaths(steamPath))
        {
            try
            {
                var steamAppsPath = Path.Combine(libraryPath, "steamapps");
                var manifestPath = Path.Combine(steamAppsPath, $"appmanifest_{appId}.acf");
                var downloadingPath = Path.Combine(steamAppsPath, "downloading", appId.ToString());

                var isInstalling = Directory.Exists(downloadingPath);
                var isInstalled = File.Exists(manifestPath);

                if (isInstalling || isInstalled)
                {
                    return new SteamGameState
                    {
                        AppId = appId,
                        IsInstalled = isInstalled,
                        IsInstalling = isInstalling,
                        ManifestPath = isInstalled ? manifestPath : null,
                        DownloadingPath = isInstalling ? downloadingPath : null,
                        LibraryPath = libraryPath
                    };
                }
            }
            catch
            {
                // One broken library path should not stop monitoring of other Steam libraries.
            }
        }

        return new SteamGameState
        {
            AppId = appId
        };
    }

    public Task<bool> CancelInstallationAsync(string steamPath, int appId, CancellationToken cancellationToken = default)
    {
        return CancelInstallationAsync(steamPath, appId, removeManifest: false, cancellationToken);
    }

    public Task<bool> CancelInstallationAsync(
        string steamPath,
        int appId,
        bool removeManifest,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CancelInstallation(steamPath, appId, removeManifest);
        }, cancellationToken);
    }

    public Task<SteamCleanupResult> CleanupInstallationAsync(
        string steamPath,
        int appId,
        SteamCleanupOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CleanupInstallation(steamPath, appId, options, cancellationToken);
        }, cancellationToken);
    }

    private SteamCleanupResult CleanupInstallation(
        string steamPath,
        int appId,
        SteamCleanupOptions options,
        CancellationToken cancellationToken)
    {
        var normalizedOptions = options ?? new SteamCleanupOptions();
        var attempts = Math.Clamp(normalizedOptions.VerificationAttempts, 1, 10);
        var removedDownloading = false;
        var removedManifest = false;
        SteamGameState lastState = new() { AppId = appId };

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = CleanupInstallationOnce(steamPath, appId, normalizedOptions.RemoveManifest);
            removedDownloading |= result.RemovedDownloadingFolder;
            removedManifest |= result.RemovedManifest;

            lastState = GetGameState(steamPath, appId);
            if (!lastState.IsInstalling && (!normalizedOptions.RemoveManifest || !lastState.IsInstalled))
            {
                return new SteamCleanupResult
                {
                    AppId = appId,
                    RemovedDownloadingFolder = removedDownloading,
                    RemovedManifest = removedManifest,
                    Verified = true,
                    Attempts = attempt,
                    Message = "Steam installation artifacts were removed and verified."
                };
            }

            if (attempt < attempts)
            {
                Thread.Sleep(Math.Clamp(normalizedOptions.VerificationDelayMilliseconds, 100, 5_000));
            }
        }

        return new SteamCleanupResult
        {
            AppId = appId,
            RemovedDownloadingFolder = removedDownloading,
            RemovedManifest = removedManifest,
            Verified = false,
            Attempts = attempts,
            RemainingDownloadingPath = lastState.DownloadingPath,
            RemainingManifestPath = normalizedOptions.RemoveManifest ? lastState.ManifestPath : null,
            Message = "Steam recreated or locked installation artifacts."
        };
    }

    private bool CancelInstallation(string steamPath, int appId, bool removeManifest)
    {
        var result = CleanupInstallationOnce(steamPath, appId, removeManifest);
        return result.RemovedAny;
    }

    private SteamCleanupResult CleanupInstallationOnce(string steamPath, int appId, bool removeManifest)
    {
        var removedAny = false;
        var removedDownloading = false;
        var removedManifest = false;

        foreach (var libraryPath in GetSteamLibraryPaths(steamPath))
        {
            var steamAppsPath = Path.Combine(libraryPath, "steamapps");
            var manifestPath = Path.Combine(steamAppsPath, $"appmanifest_{appId}.acf");
            var downloadingPath = Path.Combine(libraryPath, "steamapps", "downloading", appId.ToString());
            if (!Directory.Exists(downloadingPath))
            {
                if (removeManifest && File.Exists(manifestPath))
                {
                    var removed = TryDeleteFile(manifestPath);
                    removedAny |= removed;
                    removedManifest |= removed;
                }

                continue;
            }

            if (!IsInsideDirectory(downloadingPath, libraryPath))
            {
                continue;
            }

            // This is intentionally scoped to Steam's visible downloading folder for the selected AppID.
            // FocusGuard does not terminate processes, inject code, or alter Steam outside this path.
            var removedFolder = TryDeleteDirectory(downloadingPath);
            removedAny |= removedFolder;
            removedDownloading |= removedFolder;

            // Blocking a restricted install removes both the active download folder and the appmanifest
            // for that AppID, because Steam can keep the install queued from the manifest alone.
            if (File.Exists(manifestPath) && (removeManifest || ShouldRemoveInstallManifest(manifestPath)))
            {
                var removed = TryDeleteFile(manifestPath);
                removedAny |= removed;
                removedManifest |= removed;
            }
        }

        return new SteamCleanupResult
        {
            AppId = appId,
            RemovedDownloadingFolder = removedDownloading,
            RemovedManifest = removedManifest,
            Verified = removedAny,
            Attempts = 1
        };
    }

    private static IEnumerable<string> EnumerateSteamCandidates()
    {
        // Registry is the most reliable source when Steam was installed normally.
        foreach (var path in EnumerateRegistrySteamPaths())
        {
            yield return path;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Steam");
        }

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Steam");
        }

        foreach (var path in StandardSteamPaths)
        {
            yield return path;
        }
    }

    private static IEnumerable<string> EnumerateRegistrySteamPaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var paths = new[]
        {
            ReadRegistryValue(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            ReadRegistryValue(Registry.CurrentUser, @"Software\Valve\Steam", "InstallPath"),
            ReadRegistryValue(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            ReadRegistryValue(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath")
        };

        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            yield return path!;
        }
    }

    private static string? ReadRegistryValue(RegistryKey root, string subKey, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = root.OpenSubKey(subKey);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeCandidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            return normalized;
        }
    }

    private static IEnumerable<string> ReadLibraryFolders(string libraryFoldersPath)
    {
        string content;

        try
        {
            content = File.ReadAllText(libraryFoldersPath);
        }
        catch
        {
            yield break;
        }

        // Steam's VDF format is simple enough here: we only need "path" entries from libraryfolders.vdf.
        foreach (Match match in LibraryPathRegex().Matches(content))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static bool IsInsideDirectory(string candidatePath, string parentDirectory)
    {
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                ResetReadOnlyAttributes(path);
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(180 * (attempt + 1)));
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(180 * (attempt + 1)));
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryDeleteFile(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(120 * (attempt + 1)));
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(120 * (attempt + 1)));
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static void ResetReadOnlyAttributes(string directoryPath)
    {
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
        }
        catch
        {
            // Best effort only: locked Steam files will be retried by the caller on the next monitor tick.
        }
    }

    private static bool ShouldRemoveInstallManifest(string manifestPath)
    {
        try
        {
            var content = File.ReadAllText(manifestPath);
            var match = StateFlagsRegex().Match(content);
            if (!match.Success || !int.TryParse(match.Groups["flags"].Value, out var stateFlags))
            {
                return false;
            }

            const int InstalledStateFlag = 4;
            return (stateFlags & InstalledStateFlag) == 0;
        }
        catch
        {
            return false;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"(?<path>.+?)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex("\"StateFlags\"\\s+\"(?<flags>\\d+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex StateFlagsRegex();
}
