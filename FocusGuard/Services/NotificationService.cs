using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FocusGuard.Helpers;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using WpfApplication = System.Windows.Application;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace FocusGuard.Services;

public sealed class NotificationService : INotificationService
{
    public NotificationService()
    {
        TryPrepareNativeNotifications();
    }

    public void ShowRestrictedGameDetected(string gameName, int appId)
    {
        NotifyRestrictedGameLaunched(gameName, appId);
    }

    public void NotifySteamDetected(string steamPath)
    {
        ShowToast("Steam detected", $"FocusGuard is monitoring {steamPath}.");
    }

    public void NotifyRestrictedGameInstalling(string gameName, int appId)
    {
        ShowToast("Restricted game installing", $"{gameName} (AppID {appId}) is currently installing.");
    }

    public void NotifyRestrictedGameLaunched(string gameName, int appId)
    {
        ShowToast("Restricted game launched", $"{gameName} (AppID {appId}) is on your restricted list.");
    }

    public void NotifyFocusSessionStarted()
    {
        ShowToast("Focus session started", "Protection is active. Restricted Steam games will be watched quietly.");
    }

    public void NotifyFocusSessionEnded()
    {
        ShowToast("Focus session ended", "Protection is paused.");
    }

    public bool ConfirmCancelInstallation(string gameName, int appId)
    {
        var result = WpfMessageBox.Show(
            $"Cancel the current Steam download folder for {gameName} (AppID {appId})?\n\nThis only removes the visible steamapps/downloading/{appId} folder. It does not use hooks, drivers, injection, or hidden behavior.",
            "Cancel installation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public string? PromptForDisablePassword()
    {
        var passwordBox = new PasswordBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            MinWidth = 280
        };

        var dialog = new Window
        {
            Title = "Disable protection",
            Width = 380,
            Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(WpfColor.FromRgb(16, 18, 24)),
            Foreground = WpfBrushes.White,
            Owner = WpfApplication.Current?.MainWindow,
            Content = BuildPasswordPromptContent(passwordBox)
        };

        var confirmed = dialog.ShowDialog() == true;
        return confirmed ? passwordBox.Password : null;
    }

    public void ShowInfo(string message)
    {
        WpfMessageBox.Show(message, "FocusGuard", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message)
    {
        WpfMessageBox.Show(message, "FocusGuard", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void TryPrepareNativeNotifications()
    {
        try
        {
            ToastShortcutHelper.EnsureShortcut();
        }
        catch
        {
            // Toast preparation can fail in portable or locked-down Windows profiles.
            // The app still works; activity logs remain visible in the UI.
        }
    }

    private static void ShowToast(string title, string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var textNodes = xml.GetElementsByTagName("text");
            textNodes[0].AppendChild(xml.CreateTextNode(title));
            textNodes[1].AppendChild(xml.CreateTextNode(message));

            AddSilentAudio(xml);

            var toast = new ToastNotification(xml)
            {
                Group = "FocusGuard"
            };

            ToastNotificationManager
                .CreateToastNotifier(ToastShortcutHelper.AppUserModelId)
                .Show(toast);
        }
        catch
        {
            // Native notifications are intentionally best-effort and non-blocking.
        }
    }

    private static void AddSilentAudio(XmlDocument xml)
    {
        var audio = xml.CreateElement("audio");
        audio.SetAttribute("silent", "true");
        xml.DocumentElement?.AppendChild(audio);
    }

    private static UIElement BuildPasswordPromptContent(PasswordBox passwordBox)
    {
        var root = new StackPanel
        {
            Margin = new Thickness(22)
        };

        root.Children.Add(new TextBlock
        {
            Text = "Enter the disable password to pause protection.",
            Foreground = WpfBrushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(passwordBox);

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancelButton = new WpfButton
        {
            Content = "Cancel",
            MinWidth = 84,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var confirmButton = new WpfButton
        {
            Content = "Confirm",
            MinWidth = 92
        };

        cancelButton.Click += (_, _) => Window.GetWindow(cancelButton)!.DialogResult = false;
        confirmButton.Click += (_, _) => Window.GetWindow(confirmButton)!.DialogResult = true;

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(confirmButton);
        root.Children.Add(buttons);

        return root;
    }
}
