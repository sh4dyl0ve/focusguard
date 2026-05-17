using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Data;
using FocusGuard.Config;
using FocusGuard.Helpers;
using FocusGuard.Models;
using FocusGuard.Services;

namespace FocusGuard.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private const string SteamNotFoundWarning =
        "Steam was not found automatically. Open Settings, enter your Steam folder, then save.";

    private readonly IAppSettingsService _settingsService;
    private readonly ISteamPathDetectionService _steamDetectionService;
    private readonly ISteamProcessService _steamProcessService;
    private readonly INotificationService _notificationService;
    private readonly IGameMonitoringService _monitoringService;
    private readonly IWindowsLaunchPolicyService _windowsLaunchPolicyService;
    private readonly IWindowsFirewallService _windowsFirewallService;
    private readonly IProcessWatchdogService _processWatchdogService;   
    private readonly ILocalizationService _localizationService;
    private readonly IPasswordService _passwordService;
    private readonly ILoggingService _loggingService;
    private readonly HashSet<string> _notifiedStatusKeys = [];
    private readonly HashSet<int> _autoCancelInProgress = [];
    private readonly CancellationTokenSource _shutdown = new();
    private bool _steamDetectedNotificationShown;
    private AppSettings _settings = new();
    private string _steamPath = string.Empty;
    private string _selectedPage = "Dashboard";
    private string _newAppId = string.Empty;
    private string _newGameName = string.Empty;
    private string _newExecutableName = string.Empty;
    private string _currentLanguage = LocalizationService.English;
    private string _logSearchText = string.Empty;
    private string _disablePassword = string.Empty;
    private bool _monitoringEnabled;
    private bool _focusModeEnabled;
    private bool _autoCancelRestrictedInstallations = true;
    private bool _forcedSteamQuitEnabled;
    private bool _blockSteamNetworkDuringRestrictedInstallations = true;
    private bool _minimizeToTrayOnClose = true;
    private bool _blockInstalledGameLaunchesEnabled;
    private ProtectionMode _protectionMode = ProtectionMode.Strict;
    private bool _isBusy;
    private int _checkIntervalSeconds = 5;
    private string _steamStatus = "Steam path not checked";
    private string _busyText = string.Empty;
    private string _userMessage = string.Empty;
    private string _lastAppliedLaunchPolicyKey = "__unset";
    private bool _steamFirewallBlockApplied;
    private DateTime? _focusSessionStartedAt;
    private int _blockedAttemptsCount;
    private CancellationTokenSource? _pauseProtectionCts;
    private bool _isInitialized;
    private bool _disablePasswordChangedByUser;

    public MainViewModel(
        IAppSettingsService settingsService,
        ISteamPathDetectionService steamDetectionService,
        ISteamProcessService steamProcessService,
        INotificationService notificationService,
        IGameMonitoringService monitoringService,
        IWindowsLaunchPolicyService windowsLaunchPolicyService,
        IWindowsFirewallService windowsFirewallService,
        IProcessWatchdogService processWatchdogService,          
        ILocalizationService localizationService,
        IPasswordService passwordService,
        ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _steamDetectionService = steamDetectionService;
        _steamProcessService = steamProcessService;
        _notificationService = notificationService;
        _monitoringService = monitoringService;
        _windowsLaunchPolicyService = windowsLaunchPolicyService;
        _windowsFirewallService = windowsFirewallService;
        _processWatchdogService = processWatchdogService;  
        _localizationService = localizationService;
        _passwordService = passwordService;
        _loggingService = loggingService;

        _monitoringService.StatusesUpdated += OnStatusesUpdated;
        _loggingService.EntryWritten += OnLogEntryWritten;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _processWatchdogService.ProcessKilled += OnProcessKilled; 

        Games = [];
        ActivityLog = [];
        FilteredActivityLog = CollectionViewSource.GetDefaultView(ActivityLog);
        FilteredActivityLog.Filter = FilterLogEntry;

        SelectDashboardCommand = new RelayCommand(() => SelectedPage = "Dashboard");
        SelectActivityCommand = new RelayCommand(() => SelectedPage = "Activity");
        SelectSettingsCommand = new RelayCommand(() => SelectedPage = "Settings");
        RefreshCommand = new AsyncRelayCommand(() => RunWithBusyAsync("Refreshing game status...", RefreshStatusesAsync));
        DetectSteamCommand = new AsyncRelayCommand(() => RunWithBusyAsync("Detecting Steam...", DetectSteamPathAsync));
        SaveSettingsCommand = new AsyncRelayCommand(() => RunWithBusyAsync("Saving settings...", SaveSettingsAndRefreshAsync));
        AddGameCommand = new AsyncRelayCommand(() => RunWithBusyAsync("Adding restricted AppID...", AddGameAsync), CanAddGame);
        RemoveGameCommand = new AsyncRelayCommand(
            parameter => RunWithBusyAsync("Removing restriction...", () => RemoveGameAsync(parameter)),
            parameter => parameter is RestrictedGameViewModel);
        CancelInstallationCommand = new AsyncRelayCommand(
            parameter => RunWithBusyAsync("Cancelling installation...", () => CancelInstallationAsync(parameter)),
            parameter => parameter is RestrictedGameViewModel game && game.IsInstalling);
        SetLanguageCommand = new RelayCommand(parameter => SetLanguage(parameter?.ToString()));
        SetProtectionModeCommand = new RelayCommand(parameter => SetProtectionMode(parameter?.ToString()));
        ResetWindowsPolicyCommand = new AsyncRelayCommand(
            () => RunWithBusyAsync("Clearing Windows launch policies...", ResetWindowsPoliciesAsync));
        ResetSteamFirewallCommand = new AsyncRelayCommand(
            () => RunWithBusyAsync("Clearing Steam firewall block...", ResetSteamFirewallAsync));
        PauseProtectionCommand = new RelayCommand(ToggleProtectionPause);
        DisableProtectionCommand = new RelayCommand(DisableProtection, () => MonitoringEnabled);
    }

    public ObservableCollection<RestrictedGameViewModel> Games { get; }
    public ObservableCollection<ActivityLogEntry> ActivityLog { get; }
    public ICollectionView FilteredActivityLog { get; }

    public ICommand SelectDashboardCommand { get; }
    public ICommand SelectActivityCommand { get; }
    public ICommand SelectSettingsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DetectSteamCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand AddGameCommand { get; }
    public ICommand RemoveGameCommand { get; }
    public ICommand CancelInstallationCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand SetProtectionModeCommand { get; }
    public ICommand ResetWindowsPolicyCommand { get; }
    public ICommand ResetSteamFirewallCommand { get; }
    public ICommand PauseProtectionCommand { get; }
    public ICommand DisableProtectionCommand { get; }

    public string SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                OnPropertyChanged(nameof(IsDashboardSelected));
                OnPropertyChanged(nameof(IsActivitySelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public bool IsDashboardSelected => SelectedPage == "Dashboard";
    public bool IsActivitySelected => SelectedPage == "Activity";
    public bool IsSettingsSelected => SelectedPage == "Settings";

    public string SteamPath
    {
        get => _steamPath;
        set => SetProperty(ref _steamPath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    public string UserMessage
    {
        get => _userMessage;
        private set
        {
            if (SetProperty(ref _userMessage, value))
            {
                OnPropertyChanged(nameof(HasUserMessage));
            }
        }
    }

    public bool HasUserMessage => !string.IsNullOrWhiteSpace(UserMessage);

    public bool MonitoringEnabled
    {
        get => _monitoringEnabled;
        set
        {
            if (_isInitialized && _monitoringEnabled && !value && !ConfirmDisableProtection())
            {
                OnPropertyChanged();
                _loggingService.Info("Protection disable attempt was cancelled.");
                return;
            }

            if (!SetProperty(ref _monitoringEnabled, value))
            {
                return;
            }

            _focusSessionStartedAt = value ? DateTime.Now : null;
            OnPropertyChanged(nameof(ProtectionStatusText));
            OnPropertyChanged(nameof(HeroProtectionStatusText));
            OnPropertyChanged(nameof(FocusSessionElapsedText));
            OnPropertyChanged(nameof(SessionPillText));
            OnPropertyChanged(nameof(IsProtectionPaused));
            OnPropertyChanged(nameof(PauseProtectionActionText));
            OnPropertyChanged(nameof(QuickPauseActionText));
            OnPropertyChanged(nameof(QuickPauseActionSubText));
            RaiseProtectionCommandStates();
            if (!_isInitialized)
            {
                return;
            }

            RunInBackground(() => ApplyMonitoringStateAsync(value));
        }
    }

    public bool FocusModeEnabled
    {
        get => _focusModeEnabled;
        set
        {
            if (!SetProperty(ref _focusModeEnabled, value) || !_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            _loggingService.Info(value ? "Focus mode enabled." : "Focus mode disabled.");
        }
    }

    public bool AutoCancelRestrictedInstallations
    {
        get => _autoCancelRestrictedInstallations;
        set
        {
            if (!SetProperty(ref _autoCancelRestrictedInstallations, value) || !_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            _loggingService.Info(value
                ? "Automatic cancellation of restricted installations enabled."
                : "Automatic cancellation of restricted installations disabled.");
        }
    }

    public bool ForcedSteamQuitEnabled
    {
        get => _forcedSteamQuitEnabled;
        set
        {
            if (!SetProperty(ref _forcedSteamQuitEnabled, value) || !_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            _loggingService.Info(value
                ? "Forced Steam quit for restricted installations enabled."
                : "Forced Steam quit for restricted installations disabled.");
        }
    }

    public bool BlockSteamNetworkDuringRestrictedInstallations
    {
        get => _blockSteamNetworkDuringRestrictedInstallations;
        set
        {
            if (!SetProperty(ref _blockSteamNetworkDuringRestrictedInstallations, value) || !_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            if (!value)
            {
                RunInBackground(() => ApplySteamFirewallBlockAsync(shouldBlock: false));
            }

            _loggingService.Info(value
                ? "Steam network blocking enabled for restricted installations."
                : "Steam network blocking disabled for restricted installations.");
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set
        {
            if (!SetProperty(ref _minimizeToTrayOnClose, value) || !_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            _loggingService.Info(value
                ? "Close-to-tray behavior enabled."
                : "Close-to-tray behavior disabled.");
        }
    }

    public bool BlockInstalledGameLaunchesEnabled
    {
        get => _blockInstalledGameLaunchesEnabled;
        set
        {
            if (!SetProperty(ref _blockInstalledGameLaunchesEnabled, value) || !_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            ApplyLaunchPolicyInBackground();
            _loggingService.Info(value
                ? "Windows launch policy blocking enabled for installed restricted games."
                : "Windows launch policy blocking disabled for installed restricted games.");
        }
    }

    public ProtectionMode ProtectionMode
    {
        get => _protectionMode;
        private set
        {
            if (!SetProperty(ref _protectionMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSoftModeSelected));
            OnPropertyChanged(nameof(IsStrictModeSelected));
            OnPropertyChanged(nameof(IsLockdownModeSelected));
            OnPropertyChanged(nameof(ProtectionModeText));

            AutoCancelRestrictedInstallations = value != ProtectionMode.Soft;
            ForcedSteamQuitEnabled = value == ProtectionMode.Lockdown;

            if (!_isInitialized)
            {
                return;
            }

            RunInBackground(SaveSettingsAsync);
            _loggingService.Info($"Protection mode changed to {value}.");
        }
    }

    public bool IsSoftModeSelected => ProtectionMode == ProtectionMode.Soft;
    public bool IsStrictModeSelected => ProtectionMode == ProtectionMode.Strict;
    public bool IsLockdownModeSelected => ProtectionMode == ProtectionMode.Lockdown;
    public string ProtectionModeText => ProtectionMode switch
    {
        ProtectionMode.Soft => _localizationService.Translate("Settings.ModeSoft"),
        ProtectionMode.Lockdown => _localizationService.Translate("Settings.ModeLockdown"),
        _ => _localizationService.Translate("Settings.ModeStrict")
    };

    public int CheckIntervalSeconds
    {
        get => _checkIntervalSeconds;
        set
        {
            var normalized = Math.Clamp(value, 2, 3600);
            if (!SetProperty(ref _checkIntervalSeconds, normalized) || !_isInitialized)
            {
                return;
            }

            UpdateBackgroundMonitoring();
            RunInBackground(SaveSettingsAsync);
        }
    }

    public string NewAppId
    {
        get => _newAppId;
        set
        {
            if (SetProperty(ref _newAppId, value))
            {
                RaiseAddGameCanExecuteChanged();
            }
        }
    }

    public string NewGameName
    {
        get => _newGameName;
        set
        {
            if (SetProperty(ref _newGameName, value))
            {
                RaiseAddGameCanExecuteChanged();
            }
        }
    }

    public string NewExecutableName
    {
        get => _newExecutableName;
        set => SetProperty(ref _newExecutableName, value);
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set
        {
            if (SetProperty(ref _currentLanguage, value))
            {
                OnPropertyChanged(nameof(IsEnglishSelected));
                OnPropertyChanged(nameof(IsRussianSelected));
                OnPropertyChanged(nameof(IsChineseSelected));
            }
        }
    }

    public bool IsEnglishSelected => CurrentLanguage == LocalizationService.English;
    public bool IsRussianSelected => CurrentLanguage == LocalizationService.Russian;
    public bool IsChineseSelected => CurrentLanguage == LocalizationService.ChineseSimplified;

    public string LogSearchText
    {
        get => _logSearchText;
        set
        {
            if (SetProperty(ref _logSearchText, value))
            {
                FilteredActivityLog.Refresh();
            }
        }
    }

    public string DisablePassword
    {
        get => _disablePassword;
        set
        {
            if (!SetProperty(ref _disablePassword, value) || !_isInitialized)
            {
                return;
            }

            _disablePasswordChangedByUser = true;
            RunInBackground(SaveSettingsAsync);
        }
    }

    public string SteamStatus
    {
        get => _steamStatus;
        set => SetProperty(ref _steamStatus, value);
    }

    public int RestrictedCount => Games.Count;
    public int BlockedCount => Games.Count(game => game.Status is GameStatus.Restricted
        or GameStatus.Installing
        or GameStatus.Blocking
        or GameStatus.Blocked
        or GameStatus.BlockFailed);
    public int NotInstalledCount => Games.Count(game => game.Status == GameStatus.NotInstalled);
    public int AllowedCount => NotInstalledCount;
    public int BlockedAttemptsCount => _blockedAttemptsCount;
    public bool IsProtectionPaused => !MonitoringEnabled;
    public string PrimaryGameName => Games.FirstOrDefault()?.Name ?? "Dota 2";
    public string HeroRestrictedGameText => TranslateFormat("Hero.BlockedFormat", PrimaryGameName);
    public string HeroProtectionDetail => _localizationService.Translate("Hero.Detail");
    public string HeroProtectionStatusText => MonitoringEnabled
        ? _localizationService.Translate("Protection.Active")
        : _localizationService.Translate("Protection.Paused");
    public string FocusSessionElapsedText
    {
        get
        {
            if (!MonitoringEnabled || _focusSessionStartedAt is null)
            {
                return _localizationService.Translate("Time.Paused");
            }

            var elapsed = DateTime.Now - _focusSessionStartedAt.Value;
            if (elapsed.TotalHours >= 1)
            {
                return TranslateFormat("Time.HoursMinutesFormat", (int)elapsed.TotalHours, elapsed.Minutes);
            }

            return TranslateFormat("Time.MinutesFormat", Math.Max(0, elapsed.Minutes));
        }
    }
    public string SessionPillText => MonitoringEnabled
        ? _localizationService.Translate("Session.Active")
        : _localizationService.Translate("Session.Paused");
    public string PauseProtectionActionText => MonitoringEnabled
        ? _localizationService.Translate("Dashboard.Pause15")
        : _localizationService.Translate("Dashboard.ResumeProtection");
    public string QuickPauseActionText => MonitoringEnabled
        ? _localizationService.Translate("Right.PauseProtection")
        : _localizationService.Translate("Right.ResumeProtection");
    public string QuickPauseActionSubText => MonitoringEnabled
        ? _localizationService.Translate("Right.PauseProtectionSub")
        : _localizationService.Translate("Right.ResumeProtectionSub");
    public bool HasGames => Games.Count > 0;
    public bool HasActivityLogs => ActivityLog.Count > 0;
    public string ProtectionStatusText => MonitoringEnabled
        ? _localizationService.Translate("Protection.Active")
        : _localizationService.Translate("Protection.Paused");

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyText = "Starting FocusGuard...";
            _settings = await _settingsService.LoadAsync(_shutdown.Token);
            CurrentLanguage = _settings.Language;
            _localizationService.SetLanguage(CurrentLanguage);
            SteamPath = string.IsNullOrWhiteSpace(_settings.SteamPath)
                ? await _steamDetectionService.AutoDetectSteamPathAsync(_shutdown.Token)
                : _settings.SteamPath;
            MonitoringEnabled = _settings.MonitoringEnabled;
            if (MonitoringEnabled)
            {
                _focusSessionStartedAt = DateTime.Now;
            }
            FocusModeEnabled = _settings.FocusModeEnabled;
            AutoCancelRestrictedInstallations = _settings.AutoCancelRestrictedInstallations;
            ForcedSteamQuitEnabled = _settings.ForcedSteamQuitEnabled;
            BlockSteamNetworkDuringRestrictedInstallations = _settings.BlockSteamNetworkDuringRestrictedInstallations;
            ProtectionMode = _settings.ProtectionMode;
            MinimizeToTrayOnClose = _settings.MinimizeToTrayOnClose;
            BlockInstalledGameLaunchesEnabled = _settings.BlockInstalledGameLaunchesEnabled;
            CheckIntervalSeconds = _settings.CheckIntervalSeconds;
            _passwordService.MigrateLegacyPassword(_settings);
            DisablePassword = string.Empty;

            Games.Clear();
            foreach (var game in _settings.RestrictedGames)
            {
                Games.Add(new RestrictedGameViewModel(game.AppId, game.Name, game.ExecutableName));
            }

            _isInitialized = true;
            OnPropertyChanged(nameof(HasGames));
            OnPropertyChanged(nameof(PrimaryGameName));
            OnPropertyChanged(nameof(HeroRestrictedGameText));
            if (_steamDetectionService.IsSteamRoot(SteamPath))
            {
                NotifySteamDetectedOnce(SteamPath);
            }

            await SaveSettingsAsync();
            await RefreshStatusesAsync();

            StartBackgroundMonitoring();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            UserMessage = "FocusGuard could not finish startup. Check Activity logs for details.";
            _loggingService.Error("Application startup failed", ex);
        }
        finally
        {
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _pauseProtectionCts?.Cancel();
        _pauseProtectionCts?.Dispose();
        _monitoringService.Stop();
        _processWatchdogService.Stop();
        TryRemoveSteamFirewallBlockOnShutdown();
        _monitoringService.StatusesUpdated -= OnStatusesUpdated;
        _processWatchdogService.ProcessKilled -= OnProcessKilled;
        _loggingService.EntryWritten -= OnLogEntryWritten;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _shutdown.Dispose();
    }

    private void TryRemoveSteamFirewallBlockOnShutdown()
    {
        if (!_steamFirewallBlockApplied)
        {
            return;
        }

        try
        {
            _windowsFirewallService
                .RemoveSteamOutboundBlockAsync(CancellationToken.None)
                .Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _loggingService.Error("Could not remove Steam firewall block during shutdown.", ex);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DispatcherHelper.RunOnUiThread(() =>
        {
            OnPropertyChanged(nameof(ProtectionStatusText));
            OnPropertyChanged(nameof(HeroProtectionStatusText));
            OnPropertyChanged(nameof(HeroRestrictedGameText));
            OnPropertyChanged(nameof(HeroProtectionDetail));
            OnPropertyChanged(nameof(FocusSessionElapsedText));
            OnPropertyChanged(nameof(ProtectionModeText));
            OnPropertyChanged(nameof(SessionPillText));
            OnPropertyChanged(nameof(PauseProtectionActionText));
            OnPropertyChanged(nameof(QuickPauseActionText));
            OnPropertyChanged(nameof(QuickPauseActionSubText));
            foreach (var game in Games)
            {
                game.RefreshLocalization();
            }
        });
    }

    private void OnStatusesUpdated(object? sender, IReadOnlyList<GameStatusUpdate> updates)
    {
        DispatcherHelper.RunOnUiThread(() => ApplyStatusUpdates(updates));
    }

    private void OnProcessKilled(object? sender, ProcessKilledEventArgs e)
    {
        DispatcherHelper.RunOnUiThread(() =>
        {
            _loggingService.Info(
                $"Watchdog blocked '{e.ProcessName}' (PID {e.ProcessId}) from launching.");
            _notificationService.NotifyRestrictedGameLaunched(e.ProcessName, 0);
        });
    }

    private void OnLogEntryWritten(object? sender, ActivityLogEntry entry)
    {
        DispatcherHelper.RunOnUiThread(() =>
        {
            ActivityLog.Insert(0, entry);
            if (IsBlockedAttemptLog(entry.Message))
            {
                _blockedAttemptsCount++;
                OnPropertyChanged(nameof(BlockedAttemptsCount));
            }

            FilteredActivityLog.Refresh();
            OnPropertyChanged(nameof(HasActivityLogs));
            while (ActivityLog.Count > 100)
            {
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
            }
        });
    }

    private bool ConfirmDisableProtection()
    {
        if (!_passwordService.HasPassword(_settings))
        {
            return true;
        }

        var password = _notificationService.PromptForDisablePassword();
        return password is not null && _passwordService.Verify(_settings, password);
    }

    private async Task ApplyMonitoringStateAsync(bool enabled)
    {
        if (enabled)
        {
            _loggingService.Info("Monitoring enabled.");
            _notificationService.NotifyFocusSessionStarted();
        }
        else
        {
            _loggingService.Info("Monitoring paused.");
            _notificationService.NotifyFocusSessionEnded();
            await ApplySteamFirewallBlockAsync(shouldBlock: false);
        }

        await SaveSettingsAsync();
        UpdateBackgroundMonitoring();
        await RefreshStatusesAsync();
    }

    private async Task DetectSteamPathAsync()
    {
        var detectedPath = await _steamDetectionService.AutoDetectSteamPathAsync(_shutdown.Token);
        if (string.IsNullOrWhiteSpace(detectedPath))
        {
            SteamStatus = SteamNotFoundWarning;
            UserMessage = SteamNotFoundWarning;
            _loggingService.Info("Steam auto-detection did not find a valid path.");
            UpdateBackgroundMonitoring();
            return;
        }

        SteamPath = detectedPath;
        SteamStatus = $"Steam detected: {detectedPath}";
        UserMessage = "Steam detected successfully.";
        _loggingService.Info($"Steam detected at {detectedPath}");
        NotifySteamDetectedOnce(detectedPath);
        await SaveSettingsAsync();
        UpdateBackgroundMonitoring();
        await RefreshStatusesAsync();
    }

    private async Task RefreshStatusesAsync()
    {
        if (!_steamDetectionService.IsSteamRoot(SteamPath))
        {
            SteamStatus = SteamNotFoundWarning;
            UserMessage = SteamNotFoundWarning;
            _notifiedStatusKeys.Clear();
            foreach (var game in Games)
            {
                UpdateGame(game, GameStatus.NotInstalled, "Steam folder not detected. Expected steam.exe and steamapps.", null);
            }

            RefreshCounters();
            ApplyLaunchPolicyInBackground();
            RunInBackground(() => ApplySteamFirewallBlockAsync(shouldBlock: false));
            return;
        }

        SteamStatus = $"Monitoring {SteamPath}";
        UserMessage = string.Empty;
        var updates = await _monitoringService.CheckGamesAsync(CreateMonitoringConfiguration(), _shutdown.Token);
        ApplyStatusUpdates(updates);
    }

    private void NotifyOnceWhenRestrictedGameAppears(RestrictedGameViewModel game, GameStatus status)
    {
        if (status == GameStatus.NotInstalled)
        {
            _notifiedStatusKeys.Remove(GetNotificationKey(game.AppId, GameStatus.Installing));
            _notifiedStatusKeys.Remove(GetNotificationKey(game.AppId, GameStatus.Restricted));
        }

        if (!MonitoringEnabled)
        {
            return;
        }

        if (status == GameStatus.Installing && _notifiedStatusKeys.Add(GetNotificationKey(game.AppId, status)))
        {
            _loggingService.Info($"{game.Name} detected as Installing.");
            _notificationService.NotifyRestrictedGameInstalling(game.Name, game.AppId);
        }

        // Avoid repeatedly interrupting the user while the same restricted app remains detected.
        if (status == GameStatus.Restricted && _notifiedStatusKeys.Add(GetNotificationKey(game.AppId, status)))
        {
            _loggingService.Info($"{game.Name} detected as Restricted.");
            _notificationService.NotifyRestrictedGameLaunched(game.Name, game.AppId);
        }
    }

    private void UpdateGame(RestrictedGameViewModel game, GameStatus status, string details, string? libraryPath)
    {
        game.Status = status;
        game.Details = details;
        game.LibraryPath = libraryPath;
        game.LastCheckedAt = DateTime.Now;
        RaiseCancelInstallationCanExecuteChanged();
    }

    private static string BuildDetails(SteamGameState state)
    {
        if (state.IsInstalling && state.DownloadingPath is not null)
        {
            return $"Installing: {state.DownloadingPath}";
        }

        if (state.IsInstalled && state.ManifestPath is not null)
        {
            return $"Installed: {state.ManifestPath}";
        }

        return "Not Installed: no appmanifest or downloading folder found.";
    }

    private static string BuildCleanupDetails(SteamCleanupResult result)
    {
        var removedParts = new List<string>();
        if (result.RemovedDownloadingFolder)
        {
            removedParts.Add("download folder");
        }

        if (result.RemovedManifest)
        {
            removedParts.Add("appmanifest");
        }

        var removedText = removedParts.Count == 0 ? "no files removed" : string.Join(", ", removedParts);
        if (result.Verified)
        {
            return $"Blocked and verified after {result.Attempts} attempt(s): {removedText}.";
        }

        var remaining = result.RemainingDownloadingPath ?? result.RemainingManifestPath ?? "Steam artifacts still detected";
        return $"Block verification failed after {result.Attempts} attempt(s): {remaining}.";
    }

    private bool CanAddGame()
    {
        return int.TryParse(NewAppId, out var appId)
            && appId > 0
            && !Games.Any(game => game.AppId == appId)
            && !string.IsNullOrWhiteSpace(NewGameName);
    }

    private async Task AddGameAsync()
    {
        if (!int.TryParse(NewAppId, out var appId) || appId <= 0)
        {
            return;
        }

        var game = new RestrictedGameViewModel(appId, NewGameName.Trim(), NormalizeExecutableName(appId, NewExecutableName));
        Games.Add(game);
        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(PrimaryGameName));
        OnPropertyChanged(nameof(HeroRestrictedGameText));
        _loggingService.Info($"Added restriction: {game.Name} (AppID {game.AppId}).");
        NewAppId = string.Empty;
        NewGameName = string.Empty;
        NewExecutableName = string.Empty;
        await SaveSettingsAsync();
        UpdateBackgroundMonitoring();
        await RefreshStatusesAsync();
    }

    private async Task RemoveGameAsync(object? parameter)
    {
        if (parameter is not RestrictedGameViewModel game)
        {
            return;
        }

        Games.Remove(game);
        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(PrimaryGameName));
        OnPropertyChanged(nameof(HeroRestrictedGameText));
        _notifiedStatusKeys.Remove(GetNotificationKey(game.AppId, GameStatus.Installing));
        _notifiedStatusKeys.Remove(GetNotificationKey(game.AppId, GameStatus.Restricted));
        _loggingService.Info($"Removed restriction: {game.Name} (AppID {game.AppId}).");
        await SaveSettingsAsync();
        UpdateBackgroundMonitoring();
        ApplyLaunchPolicyInBackground();
        RefreshCounters();
    }

    private async Task CancelInstallationAsync(object? parameter)
    {
        if (parameter is not RestrictedGameViewModel game)
        {
            return;
        }

        if (!_notificationService.ConfirmCancelInstallation(game.Name, game.AppId))
        {
            return;
        }

        try
        {
            var state = await _steamDetectionService.GetGameStateAsync(SteamPath, game.AppId, _shutdown.Token);
            var result = await BlockInstallationArtifactsAsync(game, state);
            _loggingService.Info(result.Verified
                ? $"Blocked installation artifacts for {game.Name}."
                : $"Could not verify blocked installation artifacts for {game.Name}: {result.Message}");
            UserMessage = result.Verified
                ? $"{game.Name} installation was blocked and verified."
                : $"{game.Name} installation cleanup could not be verified.";
            UpdateGame(game, result.Verified ? GameStatus.Blocked : GameStatus.BlockFailed, BuildCleanupDetails(result), state.LibraryPath);
        }
        catch (Exception ex)
        {
            _loggingService.Error($"Failed to cancel installation for {game.Name}", ex);
            _notificationService.ShowError($"FocusGuard could not cancel the installation: {ex.Message}");
        }
    }

    private async Task SaveSettingsAndRefreshAsync()
    {
        await SaveSettingsAsync();
        _loggingService.Info("Settings saved.");
        UpdateBackgroundMonitoring();
        await RefreshStatusesAsync();
    }

    private async Task SaveSettingsAsync()
    {
        _settings.SteamPath = SteamPath;
        _settings.Language = CurrentLanguage;
        _settings.MonitoringEnabled = MonitoringEnabled;
        _settings.FocusModeEnabled = FocusModeEnabled;
        _settings.AutoCancelRestrictedInstallations = AutoCancelRestrictedInstallations;
        _settings.ForcedSteamQuitEnabled = ForcedSteamQuitEnabled;
        _settings.BlockSteamNetworkDuringRestrictedInstallations = BlockSteamNetworkDuringRestrictedInstallations;
        _settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        _settings.BlockInstalledGameLaunchesEnabled = BlockInstalledGameLaunchesEnabled;
        _settings.ProtectionMode = ProtectionMode;
        _settings.CheckIntervalSeconds = CheckIntervalSeconds;
        if (_disablePasswordChangedByUser)
        {
            _passwordService.SetPassword(_settings, DisablePassword);
            _disablePassword = string.Empty;
            OnPropertyChanged(nameof(DisablePassword));
            _disablePasswordChangedByUser = false;
        }
        else
        {
            _settings.DisablePassword = string.Empty;
        }
        _settings.RestrictedGames = Games
            .Select(game => new GameRestrictionSetting
            {
                AppId = game.AppId,
                Name = game.Name,
                ExecutableName = NormalizeExecutableName(game.AppId, game.ExecutableName)
            })
            .ToList();

        await _settingsService.SaveAsync(_settings, _shutdown.Token);
    }

    private void RefreshCounters()
    {
        OnPropertyChanged(nameof(RestrictedCount));
        OnPropertyChanged(nameof(BlockedCount));
        OnPropertyChanged(nameof(NotInstalledCount));
        OnPropertyChanged(nameof(AllowedCount));
        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(FocusSessionElapsedText));
        OnPropertyChanged(nameof(SessionPillText));
        RaiseAddGameCanExecuteChanged();
    }

    private async Task RunWithBusyAsync(string busyText, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyText = busyText;
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            UserMessage = "Something went wrong. Check Activity logs for details.";
            _loggingService.Error("UI action failed", ex);
        }
        finally
        {
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    private void RunInBackground(Func<Task> action)
    {
        _ = RunSilentlyAsync(action);
    }

    private void SetLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        CurrentLanguage = language;
        _localizationService.SetLanguage(language);
        RunInBackground(SaveSettingsAsync);
        _loggingService.Info($"Language changed to {CurrentLanguage}.");
    }

    private void SetProtectionMode(string? mode)
    {
        if (!Enum.TryParse<ProtectionMode>(mode, ignoreCase: true, out var protectionMode))
        {
            return;
        }

        ProtectionMode = protectionMode;
    }

    private async Task ResetWindowsPoliciesAsync()
    {
        await _windowsLaunchPolicyService.ClearFocusGuardEntriesAsync(_shutdown.Token);
        _lastAppliedLaunchPolicyKey = "__reset";
        UserMessage = "FocusGuard Windows launch policies were cleared.";
        _loggingService.Info("FocusGuard Windows launch policy entries were cleared manually.");
    }

    private async Task ResetSteamFirewallAsync()
    {
        var result = await _windowsFirewallService.RemoveSteamOutboundBlockAsync(_shutdown.Token);
        if (result.Success)
        {
            _steamFirewallBlockApplied = false;
            UserMessage = "FocusGuard Steam firewall block was cleared.";
            _loggingService.Info("FocusGuard Steam firewall block was cleared manually.");
            return;
        }

        UserMessage = "FocusGuard could not clear the Steam firewall block. Run as administrator and try again.";
        _loggingService.Error(result.Message);
    }

    private void ToggleProtectionPause()
    {
        if (MonitoringEnabled)
        {
            StartProtectionPause();
            return;
        }

        ResumeProtectionFromPause();
    }

    private void StartProtectionPause()
    {
        if (!MonitoringEnabled)
        {
            return;
        }

        _pauseProtectionCts?.Cancel();
        _pauseProtectionCts?.Dispose();
        _pauseProtectionCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var pauseToken = _pauseProtectionCts.Token;

        MonitoringEnabled = false;
        if (MonitoringEnabled)
        {
            return;
        }

        UserMessage = _localizationService.Translate("Pause.Started");
        _loggingService.Info("Protection paused for 15 minutes.");

        RunInBackground(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), pauseToken);
                if (!_shutdown.IsCancellationRequested && !MonitoringEnabled)
                {
                    DispatcherHelper.RunOnUiThread(ResumeProtectionFromPause);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void ResumeProtectionFromPause()
    {
        _pauseProtectionCts?.Cancel();
        MonitoringEnabled = true;
        UserMessage = _localizationService.Translate("Pause.Ended");
        _loggingService.Info("Protection resumed.");
    }

    private void DisableProtection()
    {
        _pauseProtectionCts?.Cancel();
        MonitoringEnabled = false;
    }

    private async Task RunSilentlyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _loggingService.Error("Background UI operation failed", ex);
        }
    }

    private void ApplyStatusUpdates(IReadOnlyList<GameStatusUpdate> updates)
    {
        foreach (var update in updates)
        {
            var game = Games.FirstOrDefault(game => game.AppId == update.AppId);
            if (game is null)
            {
                continue;
            }

            UpdateGame(game, update.Status, BuildDetails(update.State), update.State.LibraryPath);
            NotifyOnceWhenRestrictedGameAppears(game, update.Status);
            TryAutoCancelRestrictedInstallation(game, update.Status, update.State);
        }

        RefreshCounters();
        ApplyLaunchPolicyInBackground();
        ApplySteamFirewallForStatuses(updates);
    }

    private void ApplyLaunchPolicyInBackground()
    {
        var executableNames = GetLaunchPolicyExecutableNames();

        // Keep the in-memory watchdog fresh on every status pass. Registry policy changes
        // are deduplicated below, but the watchdog should self-heal if its loop was stopped.
        _processWatchdogService.Apply(executableNames);

        var policyKey = string.Join('|', executableNames);
        if (policyKey == _lastAppliedLaunchPolicyKey)
        {
            return;
        }

        _lastAppliedLaunchPolicyKey = policyKey;
        RunInBackground(() => ApplyLaunchPolicyAsync(executableNames));
    }

    private async Task ApplyLaunchPolicyAsync(IReadOnlyCollection<string> executableNames)
    {
        await _windowsLaunchPolicyService.ApplyBlockedExecutablesAsync(executableNames, _shutdown.Token);
        _loggingService.Info(executableNames.Count == 0
            ? "Windows launch policy has no FocusGuard entries."
            : $"Windows launch policy updated for: {string.Join(", ", executableNames)}.");
    }

    private void ApplySteamFirewallForStatuses(IReadOnlyList<GameStatusUpdate> updates)
    {
        var shouldBlock = MonitoringEnabled
            && BlockSteamNetworkDuringRestrictedInstallations
            && ProtectionMode != ProtectionMode.Soft
            && updates.Any(update => update.Status == GameStatus.Installing);

        RunInBackground(() => ApplySteamFirewallBlockAsync(shouldBlock));
    }

    private async Task ApplySteamFirewallBlockAsync(bool shouldBlock)
    {
        if (shouldBlock == _steamFirewallBlockApplied)
        {
            return;
        }

        if (shouldBlock)
        {
            var steamExePath = Path.Combine(SteamPath, "steam.exe");
            var result = await _windowsFirewallService.BlockSteamOutboundAsync(steamExePath, _shutdown.Token);
            _steamFirewallBlockApplied = result.Success;

            if (!result.Success)
            {
                DispatcherHelper.RunOnUiThread(() =>
                    UserMessage = "FocusGuard could not block Steam network traffic. Run as administrator to enable firewall blocking.");
                _loggingService.Error(result.Message);
            }

            return;
        }

        var removeResult = await _windowsFirewallService.RemoveSteamOutboundBlockAsync(_shutdown.Token);
        if (removeResult.Success)
        {
            _steamFirewallBlockApplied = false;
        }
        else
        {
            _loggingService.Error(removeResult.Message);
        }
    }

    private IReadOnlyList<string> GetLaunchPolicyExecutableNames()
    {
        if (!MonitoringEnabled || !BlockInstalledGameLaunchesEnabled)
        {
            return [];
        }

        return Games
            .Select(game => NormalizeExecutableName(game.ExecutableName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void TryAutoCancelRestrictedInstallation(RestrictedGameViewModel game, GameStatus status, SteamGameState state)
    {
        if (!MonitoringEnabled
            || status != GameStatus.Installing
            || string.IsNullOrWhiteSpace(SteamPath)
            || ProtectionMode == ProtectionMode.Soft
            || _autoCancelInProgress.Contains(game.AppId))
        {
            return;
        }

        _autoCancelInProgress.Add(game.AppId);
        UpdateGame(game, GameStatus.Blocking, "Restricted installation detected. FocusGuard is blocking it...", state.LibraryPath);
        _loggingService.Info($"Protection is blocking restricted installation: {game.Name} (AppID {game.AppId}).");

        RunInBackground(async () =>
        {
            try
            {
                var result = await BlockInstallationArtifactsAsync(game, state);
                if (result.Verified)
                {
                    DispatcherHelper.RunOnUiThread(() =>
                    {
                        UpdateGame(game, GameStatus.Blocked, BuildCleanupDetails(result), state.LibraryPath);
                        UserMessage = $"{game.Name} installation was blocked and verified.";
                    });
                    _loggingService.Info($"Restricted installation blocked and verified: {game.Name} (AppID {game.AppId}).");
                    _notificationService.ShowInfo($"{game.Name} installation was blocked.");
                }
                else
                {
                    DispatcherHelper.RunOnUiThread(() =>
                    {
                        UpdateGame(game, GameStatus.BlockFailed, BuildCleanupDetails(result), state.LibraryPath);
                        UserMessage = $"{game.Name} could not be fully blocked. Steam may still be holding files.";
                    });
                    _loggingService.Info($"Restricted installation block failed verification for {game.Name}: {result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                DispatcherHelper.RunOnUiThread(() =>
                {
                    UpdateGame(game, GameStatus.BlockFailed, $"Block failed: {ex.Message}", state.LibraryPath);
                    UserMessage = $"FocusGuard could not block {game.Name}. Pause or cancel it in Steam, then try again.";
                });
                _loggingService.Error($"Failed to block restricted installation for {game.Name}", ex);
                _notificationService.ShowError($"FocusGuard could not block {game.Name}: {ex.Message}");
            }
            finally
            {
                DispatcherHelper.RunOnUiThread(() => _autoCancelInProgress.Remove(game.AppId));
            }
        });
    }

    private async Task<SteamCleanupResult> BlockInstallationArtifactsAsync(RestrictedGameViewModel game, SteamGameState state)
    {
        if (BlockSteamNetworkDuringRestrictedInstallations)
        {
            DispatcherHelper.RunOnUiThread(() => UserMessage = $"Blocking Steam network traffic before stopping {game.Name}...");
            await ApplySteamFirewallBlockAsync(shouldBlock: true);
        }

        if (ProtectionMode == ProtectionMode.Lockdown)
        {
            DispatcherHelper.RunOnUiThread(() => UserMessage = $"Closing Steam before blocking {game.Name}...");
            _loggingService.Info($"Forced Steam quit requested for restricted installation: {game.Name} (AppID {game.AppId}).");

            var quitResult = await _steamProcessService.QuitSteamAsync(_shutdown.Token);
            if (quitResult.WasRunning)
            {
                _loggingService.Info(
                    $"Steam quit result: found {quitResult.FoundProcessCount}, closed {quitResult.ClosedProcessCount}, force killed {quitResult.ForceKilledProcessCount}.");
            }
            else
            {
                _loggingService.Info("Forced Steam quit requested, but Steam was not running.");
            }
        }

        return await _steamDetectionService.CleanupInstallationAsync(
            SteamPath,
            game.AppId,
            new SteamCleanupOptions
            {
                RemoveManifest = !state.IsInstalled,
                VerificationAttempts = ProtectionMode == ProtectionMode.Lockdown ? 5 : 3,
                VerificationDelayMilliseconds = ProtectionMode == ProtectionMode.Lockdown ? 650 : 450
            },
            cancellationToken: _shutdown.Token);
    }

    private void StartBackgroundMonitoring()
    {
        _monitoringService.Start(CreateMonitoringConfiguration());
    }

    private void UpdateBackgroundMonitoring()
    {
        if (_monitoringService.IsRunning)
        {
            _monitoringService.Update(CreateMonitoringConfiguration());
        }
    }

    private GameMonitoringConfiguration CreateMonitoringConfiguration()
    {
        return new GameMonitoringConfiguration
        {
            SteamPath = SteamPath,
            ProtectionEnabled = MonitoringEnabled,
            IntervalSeconds = CheckIntervalSeconds,
            AppIds = Games.Select(game => game.AppId).ToArray()
        };
    }

    private void NotifySteamDetectedOnce(string steamPath)
    {
        if (_steamDetectedNotificationShown)
        {
            return;
        }

        _steamDetectedNotificationShown = true;
        _notificationService.NotifySteamDetected(steamPath);
    }

    private static string GetNotificationKey(int appId, GameStatus status)
    {
        return $"{appId}:{status}";
    }

    private static string NormalizeExecutableName(string executableName)
    {
        return NormalizeExecutableName(0, executableName);
    }

    private static string NormalizeExecutableName(int appId, string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return appId == 570 ? "dota2.exe" : string.Empty;
        }

        executableName = Path.GetFileName(executableName.Trim());
        return executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executableName
            : $"{executableName}.exe";
    }

    private static bool IsBlockedAttemptLog(string message)
    {
        return message.Contains("blocked", StringComparison.OrdinalIgnoreCase)
            || message.Contains("blocking restricted installation", StringComparison.OrdinalIgnoreCase)
            || message.Contains("заблок", StringComparison.OrdinalIgnoreCase);
    }

    private string TranslateFormat(string key, params object[] args)
    {
        return string.Format(_localizationService.Translate(key), args);
    }

    private bool FilterLogEntry(object item)
    {
        if (item is not ActivityLogEntry entry || string.IsNullOrWhiteSpace(LogSearchText))
        {
            return true;
        }

        return entry.Message.Contains(LogSearchText, StringComparison.OrdinalIgnoreCase)
            || entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss").Contains(LogSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseAddGameCanExecuteChanged()
    {
        (AddGameCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RaiseProtectionCommandStates()
    {
        (DisableProtectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RaiseCancelInstallationCanExecuteChanged()
    {
        (CancelInstallationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
