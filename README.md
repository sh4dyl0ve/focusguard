# FocusGuard

FocusGuard is a small Windows desktop app I built for a very personal reason: I have struggled with Dota 2.

I would uninstall it, feel good for a few hours, then open Steam and install it again almost automatically. The problem was not that I did not know what I wanted. The problem was that, in a weak moment, reinstalling the game was too easy.

FocusGuard is my attempt to put a little friction between my current self and the habit I am trying to leave behind. It is not meant to be a security product, anti-cheat tool, or invisible blocker. It is a visible, user-controlled focus tool for people who want to make distracting Steam games harder to reinstall or launch.

The default restricted game is Dota 2, AppID `570`, but any Steam AppID can be added.

## Download

Download the latest Windows build from the GitHub Releases page:

[FocusGuard releases](https://github.com/sh4dyl0ve/FocusGuard/releases)

For most users, download:

`FocusGuard-win-x64.zip`

Extract the zip and run `FocusGuard.exe`.

## What It Does

FocusGuard monitors selected Steam AppIDs and helps prevent restricted games from being installed or launched again.

It can:

- Detect the Steam install path automatically.
- Read Steam library folders from `steamapps/libraryfolders.vdf`.
- Check whether a restricted game is installed or currently installing.
- Watch these Steam paths:
  - `steamapps/appmanifest_{AppId}.acf`
  - `steamapps/downloading/{AppId}/`
- Show clear game statuses:
  - `Not Installed`
  - `Installed`
  - `Installing`
  - `Restricted`
- Remove Steam download artifacts for restricted games in stricter modes.
- Optionally close Steam when a restricted installation is detected.
- Optionally block Steam outbound traffic through Windows Firewall while a restricted game is being downloaded.
- Optionally stop restricted game executables such as `dota2.exe`.
- Optionally apply Windows launch policy rules for installed restricted games.
- Keep an activity log so the app stays transparent about what it did.

## Why Administrator Mode Is Recommended

FocusGuard works best when launched as Administrator.

Some features need elevated Windows permissions:

- Windows Firewall rules for blocking Steam network access.
- Windows launch policy changes.
- Removing files that Steam may currently own or lock.
- Stopping game processes reliably.

The app can still open without admin rights, but strict blocking features may fail or work only partially. If something looks like it was detected but not blocked, restart FocusGuard with `Run as administrator`.

## Main Screens

- `Dashboard` - current protection state, restricted games, Steam status, and recent activity.
- `Settings` - Steam path, AppID restrictions, protection modes, language, password, tray behavior, and advanced blocking options.
- `Activity logs` - timestamped history of detected installs, blocked attempts, Steam detection, language changes, firewall events, and process stops.

## Protection Modes

FocusGuard is intentionally visible and configurable. The idea is not to hide from the user, but to help the user keep a promise they already made.

Typical setup:

1. Add the Steam AppID you want to restrict.
2. Enable protection.
3. Enable stricter options only if you want stronger friction.
4. Run the app as Administrator for firewall and launch-policy features.
5. Keep tray mode enabled so FocusGuard stays active in the background.

## Safety Scope

FocusGuard is a productivity and digital wellbeing app, not a security tool.

It does not use:

- low-level keyboard or mouse hooks
- memory access
- anti-cheat interaction
- kernel drivers
- DLL injection
- stealth behavior
- hidden persistence

Everything is ordinary desktop-app behavior: file checks, visible settings, Windows Firewall rules when enabled, Windows policy settings when enabled, and normal process monitoring.

## Tech Stack

- C#
- .NET 8
- WPF
- MVVM
- Dependency Injection
- Windows 10/11

## Build From Source

Open `FocusGuard.sln` on Windows with Visual Studio 2022 or newer.

Or run:

```powershell
dotnet build .\FocusGuard.sln -c Release
```

Run smoke tests:

```powershell
dotnet run --project .\FocusGuard.Tests\FocusGuard.Tests.csproj -c Release
```

Create a self-contained Windows x64 build:

```powershell
dotnet publish .\FocusGuard\FocusGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -o .\artifacts\FocusGuard-win-x64
```

## Project Structure

- `Config` - persisted application settings models.
- `Helpers` - small UI/runtime helpers.
- `Models` - domain models and status values.
- `Services` - service interfaces and implementations for settings, Steam detection, monitoring, logging, notifications, firewall, and launch policy.
- `Styles` - WPF resource dictionaries, theme tokens, and localization strings.
- `ViewModels` - MVVM state and commands.
- `Views` - WPF windows and visual surfaces.

## A Note On Intent

FocusGuard is not supposed to “beat” Steam or Windows. It is supposed to make relapse less automatic.

If you are trying to stop reinstalling a game you keep coming back to, the app gives you a moment to pause. Sometimes that moment is enough.
