using System.IO;
using Microsoft.Win32;

namespace FocusGuard.Services;

public sealed class WindowsLaunchPolicyService : IWindowsLaunchPolicyService
{
    private const string ExplorerPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private const string DisallowRunSubKeyName = "DisallowRun";
    private const string MetadataKeyPath = @"Software\FocusGuard\LaunchPolicy";
    private const string OwnedValueNamesValue = "OwnedDisallowRunValueNames";

    private readonly ILoggingService _loggingService;

    public WindowsLaunchPolicyService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public Task ApplyBlockedExecutablesAsync(
        IReadOnlyCollection<string> executableNames,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyBlockedExecutables(executableNames);
        }, cancellationToken);
    }

    public Task ClearFocusGuardEntriesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyBlockedExecutables([]);
        }, cancellationToken);
    }

    private void ApplyBlockedExecutables(IReadOnlyCollection<string> executableNames)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var normalizedExecutables = executableNames
            .Select(NormalizeExecutableName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var explorerKey = Registry.CurrentUser.CreateSubKey(ExplorerPolicyPath, writable: true);
        using var disallowRunKey = explorerKey?.CreateSubKey(DisallowRunSubKeyName, writable: true);
        using var metadataKey = Registry.CurrentUser.CreateSubKey(MetadataKeyPath, writable: true);

        if (explorerKey is null || disallowRunKey is null || metadataKey is null)
        {
            _loggingService.Error("Windows launch policy registry keys could not be opened.");
            return;
        }

        ClearOwnedPolicyValues(disallowRunKey, metadataKey);

        if (normalizedExecutables.Length == 0)
        {
            UpdatePolicyFlag(explorerKey, disallowRunKey);
            metadataKey.DeleteValue(OwnedValueNamesValue, throwOnMissingValue: false);
            return;
        }

        var ownedValueNames = new List<string>();
        var reservedValueNames = disallowRunKey
            .GetValueNames()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var executableName in normalizedExecutables)
        {
            var valueName = FindNextPolicyValueName(reservedValueNames);
            disallowRunKey.SetValue(valueName, executableName, RegistryValueKind.String);
            reservedValueNames.Add(valueName);
            ownedValueNames.Add(valueName);
        }

        metadataKey.SetValue(OwnedValueNamesValue, string.Join(';', ownedValueNames), RegistryValueKind.String);
        explorerKey.SetValue(DisallowRunSubKeyName, 1, RegistryValueKind.DWord);
    }

    private static void ClearOwnedPolicyValues(RegistryKey disallowRunKey, RegistryKey metadataKey)
    {
        foreach (var valueName in GetOwnedValueNames(metadataKey))
        {
            disallowRunKey.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static IReadOnlyList<string> GetOwnedValueNames(RegistryKey metadataKey)
    {
        var rawValueNames = metadataKey.GetValue(OwnedValueNamesValue)?.ToString();
        if (string.IsNullOrWhiteSpace(rawValueNames))
        {
            return [];
        }

        return rawValueNames
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(valueName => int.TryParse(valueName, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FindNextPolicyValueName(ISet<string> reservedValueNames)
    {
        for (var valueNumber = 1; valueNumber < 10_000; valueNumber++)
        {
            var valueName = valueNumber.ToString();
            if (!reservedValueNames.Contains(valueName))
            {
                return valueName;
            }
        }

        throw new InvalidOperationException("No free DisallowRun registry value names are available.");
    }

    private static void UpdatePolicyFlag(RegistryKey explorerKey, RegistryKey disallowRunKey)
    {
        if (disallowRunKey.GetValueNames().Length == 0)
        {
            explorerKey.SetValue(DisallowRunSubKeyName, 0, RegistryValueKind.DWord);
        }
    }

    private static string NormalizeExecutableName(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return string.Empty;
        }

        executableName = Path.GetFileName(executableName.Trim());
        return executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executableName
            : $"{executableName}.exe";
    }
}
