# FocusGuard

FocusGuard is a Windows desktop MVP for digital wellbeing and parental-control style Steam restrictions.

## Tech stack

- C# / .NET 8
- WPF
- MVVM
- Dependency Injection
- Windows 10/11

## Release features

- Automatically detects the Steam installation path from the Windows registry and common install folders.
- Reads Steam library folders from `steamapps/libraryfolders.vdf`.
- Checks restricted Steam AppIDs using:
  - `steamapps/appmanifest_{AppId}.acf`
  - `steamapps/downloading/{AppId}/`
- Shows status values:
  - `Not Installed`
  - `Installed`
  - `Installing`
  - `Restricted`
- Starts with Dota 2 as the default restricted game:
  - Dota 2, AppID `570`
- Lets the user add or remove restricted Steam AppIDs.
- Shows a premium dark dashboard, activity log, settings screen, tray mode, and EN/RU/ZH-Hans localization.
- Can clean Steam installation artifacts for restricted games when strict/lockdown mode is enabled.
- Can optionally block Steam outbound traffic through Windows Firewall while a restricted installation is detected.
- Can optionally close Steam during restricted installs and stop restricted game executables such as `dota2.exe`.
- Can optionally apply the Windows `DisallowRun` launch policy for installed restricted games.
- Supports a password prompt before disabling protection.

## Safety scope

This is a productivity and focus application, not a security or anti-cheat tool. It does not use low-level hooks, memory access, anti-cheat interaction, kernel drivers, DLL injection, persistence, or stealth behavior.

## Run

Open `FocusGuard.sln` on Windows with Visual Studio 2022 or newer, or run:

```powershell
dotnet run --project .\FocusGuard\FocusGuard.csproj
```

## Publish

Create a self-contained Windows x64 build:

```powershell
dotnet publish .\FocusGuard\FocusGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -o .\artifacts\FocusGuard-win-x64
```

## Verify

Run the smoke tests:

```powershell
dotnet run --project .\FocusGuard.Tests\FocusGuard.Tests.csproj -c Release
```

## Project structure

- `Config` - persisted application settings models.
- `Helpers` - small UI/runtime helpers.
- `Models` - domain models and status values.
- `Services` - service interfaces and implementations for settings, Steam detection, monitoring, logging, and notifications.
- `Styles` - WPF resource dictionaries.
- `ViewModels` - MVVM state and commands.
- `Views` - WPF windows and visual surfaces.
