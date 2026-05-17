using System.Windows;
using FocusGuard.Services;
using FocusGuard.ViewModels;
using FocusGuard.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FocusGuard;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _serviceProvider = ConfigureServices();
        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggingService, ActivityLoggingService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<ISteamPathDetectionService, SteamDetectionService>();
        services.AddSingleton<ISteamProcessService, SteamProcessService>();
        services.AddSingleton<IGameMonitoringService, GameMonitoringService>();
        services.AddSingleton<IWindowsLaunchPolicyService, WindowsLaunchPolicyService>();
        services.AddSingleton<IWindowsFirewallService, WindowsFirewallService>();
        services.AddSingleton<IProcessWatchdogService, ProcessWatchdogService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<INotificationService, NotificationService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
