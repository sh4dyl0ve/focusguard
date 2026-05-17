using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FocusGuard.ViewModels;
using DrawingIcon = System.Drawing.Icon;
using WpfApplication = System.Windows.Application;
using WinForms = System.Windows.Forms;

namespace FocusGuard.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private WinForms.NotifyIcon? _notifyIcon;
    private DrawingIcon? _trayIcon;
    private bool _isSyncingPassword;
    private bool _isExitRequested;
    private bool _trayHintShown;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        InitializeTrayIcon();
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        _isSyncingPassword = true;
        DisablePasswordBox.Password = _viewModel.DisablePassword;
        _isSyncingPassword = false;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        DisposeTrayIcon();
        _viewModel.Dispose();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested || !_viewModel.MinimizeToTrayOnClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeRestoreIcon();
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTrayOnClose)
        {
            HideToTray();
        }
    }

    private void DisablePasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        _viewModel.DisablePassword = DisablePasswordBox.Password;
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse state changes while WPF enters the drag loop.
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ScrollableContent_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindParent<ScrollViewer>(sender as DependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaximizeRestoreIcon();
    }

    private void UpdateMaximizeRestoreIcon()
    {
        if (!IsLoaded)
        {
            return;
        }

        MaximizeRestoreButton.Content = WindowState == WindowState.Maximized
            ? "\uE923"
            : "\uE922";
    }

    private static T? FindParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child is not null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T parent)
            {
                return parent;
            }
        }

        return null;
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = LoadTrayIcon();
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "FocusGuard",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private WinForms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open FocusGuard", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Exit", null, (_, _) => ExitFromTray());
        return menu;
    }

    private DrawingIcon LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/FocusGuard.ico"));
        if (resource?.Stream is not null)
        {
            using var stream = resource.Stream;
            return new DrawingIcon(stream);
        }

        try
        {
            return DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? System.Drawing.SystemIcons.Application;
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();

        if (_trayHintShown || _notifyIcon is null)
        {
            return;
        }

        _trayHintShown = true;
        _notifyIcon.BalloonTipTitle = "FocusGuard is still running";
        _notifyIcon.BalloonTipText = "Double-click the tray icon to reopen it, or use Exit from the tray menu.";
        _notifyIcon.ShowBalloonTip(2500);
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _isExitRequested = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }

        Close();
    }

    private void DisposeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
