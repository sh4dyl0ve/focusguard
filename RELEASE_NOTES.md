# FocusGuard v1.0.0

Initial public release of FocusGuard.

## Highlights

- Premium WPF desktop UI with dark theme, dashboard, settings, and activity logs.
- Steam path auto-detection through Windows Registry and common install paths.
- Steam AppID monitoring for restricted games.
- Detection of installed and actively installing Steam games through `appmanifest_*.acf` and `steamapps/downloading/{AppId}`.
- Strict/lockdown controls for removing restricted installation artifacts.
- Optional Steam outbound firewall block during restricted installation attempts.
- Optional forced Steam quit and restricted process watchdog.
- Optional Windows launch policy for installed restricted games.
- Tray mode, Windows notifications, password prompt, and EN/RU/ZH-Hans localization.

## Download

Use the `FocusGuard-win-x64.zip` asset attached to this release.

## Notes

Some protection features require administrator permissions on Windows because they use Windows Firewall or Windows launch policy APIs.

Built from the public `main` branch.
