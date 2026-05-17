using System.Diagnostics;
using System.IO;
using FocusGuard.Models;

namespace FocusGuard.Services;

public sealed class WindowsFirewallService : IWindowsFirewallService
{
    private const string RuleName = "FocusGuard - Block Steam Outbound";
    private readonly ILoggingService _loggingService;

    public WindowsFirewallService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<FirewallRuleResult> BlockSteamOutboundAsync(
        string steamExecutablePath,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FirewallRuleResult { Success = true, Message = "Windows Firewall is only applied on Windows." };
        }

        if (string.IsNullOrWhiteSpace(steamExecutablePath) || !File.Exists(steamExecutablePath))
        {
            return new FirewallRuleResult
            {
                Success = false,
                Message = "Steam executable was not found, so the firewall rule was not applied."
            };
        }

        await DeleteRuleIfPresentAsync(cancellationToken);

        var result = await RunNetshAsync(
            [
                "advfirewall",
                "firewall",
                "add",
                "rule",
                $"name={RuleName}",
                "dir=out",
                "action=block",
                $"program={Path.GetFullPath(steamExecutablePath)}",
                "enable=yes",
                "profile=any"
            ],
            cancellationToken);

        if (result.Success)
        {
            _loggingService.Info("Windows Firewall is blocking Steam outbound traffic for restricted installation control.");
            return result.WithMessage("Steam outbound traffic is blocked by FocusGuard.");
        }

        _loggingService.Error($"Could not apply Steam firewall block: {result.Message}");
        return result;
    }

    public async Task<FirewallRuleResult> RemoveSteamOutboundBlockAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FirewallRuleResult { Success = true, Message = "Windows Firewall is only applied on Windows." };
        }

        var result = await DeleteRuleIfPresentAsync(cancellationToken);
        if (result.Success)
        {
            _loggingService.Info("FocusGuard Steam firewall block was removed.");
        }

        return result;
    }

    private async Task<FirewallRuleResult> DeleteRuleIfPresentAsync(CancellationToken cancellationToken)
    {
        var result = await RunNetshAsync(
            ["advfirewall", "firewall", "delete", "rule", $"name={RuleName}"],
            cancellationToken);

        // netsh returns a non-zero exit code when the rule does not exist. For our owned
        // rule that is an acceptable final state because Steam traffic is not blocked.
        if (!result.Success && !IsPermissionFailure(result.Output))
        {
            return new FirewallRuleResult
            {
                Success = true,
                Changed = false,
                Message = "FocusGuard Steam firewall rule was already absent.",
                ExitCode = result.ExitCode,
                Output = result.Output
            };
        }

        return result.Success
            ? result.WithMessage("FocusGuard Steam firewall rule was removed.")
            : result;
    }

    private static async Task<FirewallRuleResult> RunNetshAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var output = string.Join(Environment.NewLine, await outputTask, await errorTask).Trim();
            var success = process.ExitCode == 0;

            return new FirewallRuleResult
            {
                Success = success,
                Changed = success,
                ExitCode = process.ExitCode,
                Output = output,
                Message = success
                    ? "Windows Firewall command completed."
                    : BuildFailureMessage(process.ExitCode, output)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new FirewallRuleResult
            {
                Success = false,
                Message = $"Windows Firewall command failed: {ex.Message}",
                Output = ex.ToString(),
                ExitCode = -1
            };
        }
    }

    private static string BuildFailureMessage(int exitCode, string output)
    {
        if (IsPermissionFailure(output))
        {
            return "Administrator rights are required to change Windows Firewall rules.";
        }

        return string.IsNullOrWhiteSpace(output)
            ? $"Windows Firewall command failed with exit code {exitCode}."
            : output;
    }

    private static bool IsPermissionFailure(string output)
    {
        return output.Contains("requires elevation", StringComparison.OrdinalIgnoreCase)
            || output.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
            || output.Contains("отказано в доступе", StringComparison.OrdinalIgnoreCase)
            || output.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase);
    }
}

file static class FirewallRuleResultExtensions
{
    public static FirewallRuleResult WithMessage(this FirewallRuleResult result, string message)
    {
        return new FirewallRuleResult
        {
            Success = result.Success,
            Changed = result.Changed,
            Message = message,
            ExitCode = result.ExitCode,
            Output = result.Output
        };
    }
}
