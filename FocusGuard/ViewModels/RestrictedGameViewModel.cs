using FocusGuard.Models;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace FocusGuard.ViewModels;

public sealed class RestrictedGameViewModel : ViewModelBase
{
    private string _name;
    private string _executableName;
    private GameStatus _status;
    private string _details;
    private string? _libraryPath;
    private DateTime _lastCheckedAt;

    public RestrictedGameViewModel(int appId, string name, string executableName = "")
    {
        AppId = appId;
        _name = name;
        _executableName = executableName;
        _status = GameStatus.NotInstalled;
        _details = "Not detected";
        _lastCheckedAt = DateTime.Now;
    }

    public int AppId { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ExecutableName
    {
        get => _executableName;
        set => SetProperty(ref _executableName, value);
    }

    public GameStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsInstalling));
            }
        }
    }

    public string StatusText => WpfApplication.Current?.TryFindResource($"Status.{Status}")?.ToString()
        ?? (Status == GameStatus.NotInstalled ? "Not Installed" : Status.ToString());

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public string? LibraryPath
    {
        get => _libraryPath;
        set => SetProperty(ref _libraryPath, value);
    }

    public DateTime LastCheckedAt
    {
        get => _lastCheckedAt;
        set
        {
            if (SetProperty(ref _lastCheckedAt, value))
            {
                OnPropertyChanged(nameof(LastCheckedText));
            }
        }
    }

    public string LastCheckedText => LastCheckedAt.ToString("HH:mm:ss");

    public bool IsInstalling => Status == GameStatus.Installing;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(StatusText));
    }
}
