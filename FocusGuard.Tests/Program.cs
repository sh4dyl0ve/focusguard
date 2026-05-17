using FocusGuard.Models;
using FocusGuard.Services;

var tests = new SteamCleanupTests();
tests.CleanupRemovesDownloadingFolderAndManifest();
tests.CleanupPreservesManifestForInstalledGameWhenRequested();
tests.DetectsLibraryFolderInstallations();

Console.WriteLine("FocusGuard.Tests: all tests passed.");

internal sealed class SteamCleanupTests
{
    public void CleanupRemovesDownloadingFolderAndManifest()
    {
        using var fixture = SteamFixture.Create();
        fixture.CreateInstallingGame(570, installed: false);

        var service = new SteamDetectionService();
        var result = service.CleanupInstallationAsync(
            fixture.SteamRoot,
            570,
            new SteamCleanupOptions
            {
                RemoveManifest = true,
                VerificationAttempts = 2,
                VerificationDelayMilliseconds = 100
            }).GetAwaiter().GetResult();

        Assert(result.Verified, "cleanup should verify removed install artifacts");
        Assert(result.RemovedDownloadingFolder, "download folder should be removed");
        Assert(result.RemovedManifest, "manifest should be removed");
        Assert(!Directory.Exists(fixture.DownloadingPath(570)), "downloading folder still exists");
        Assert(!File.Exists(fixture.ManifestPath(570)), "manifest still exists");
    }

    public void CleanupPreservesManifestForInstalledGameWhenRequested()
    {
        using var fixture = SteamFixture.Create();
        fixture.CreateInstallingGame(570, installed: true);

        var service = new SteamDetectionService();
        var result = service.CleanupInstallationAsync(
            fixture.SteamRoot,
            570,
            new SteamCleanupOptions
            {
                RemoveManifest = false,
                VerificationAttempts = 2,
                VerificationDelayMilliseconds = 100
            }).GetAwaiter().GetResult();

        Assert(result.Verified, "installed game update cleanup should verify once downloading folder is gone");
        Assert(result.RemovedDownloadingFolder, "download folder should be removed");
        Assert(!result.RemovedManifest, "installed manifest should be preserved");
        Assert(File.Exists(fixture.ManifestPath(570)), "installed manifest should still exist");
    }

    public void DetectsLibraryFolderInstallations()
    {
        using var fixture = SteamFixture.Create();
        var library = fixture.CreateLibrary("library-a");
        fixture.WriteLibraryFolders(library);
        fixture.CreateInstallingGame(730, installed: false, libraryRoot: library);

        var service = new SteamDetectionService();
        var state = service.GetGameStateAsync(fixture.SteamRoot, 730).GetAwaiter().GetResult();

        Assert(state.IsInstalling, "libraryfolder installing game should be detected");
        Assert(state.LibraryPath == library, "detected library path should match secondary library");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed class SteamFixture : IDisposable
{
    private readonly string _root;

    private SteamFixture(string root)
    {
        _root = root;
        SteamRoot = Path.Combine(root, "Steam");
        Directory.CreateDirectory(Path.Combine(SteamRoot, "steamapps"));
        File.WriteAllText(Path.Combine(SteamRoot, "steam.exe"), string.Empty);
    }

    public string SteamRoot { get; }

    public static SteamFixture Create()
    {
        return new SteamFixture(Path.Combine(Path.GetTempPath(), $"focusguard-tests-{Guid.NewGuid():N}"));
    }

    public string CreateLibrary(string name)
    {
        var library = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(library, "steamapps"));
        return library;
    }

    public void WriteLibraryFolders(string libraryRoot)
    {
        var escaped = libraryRoot.Replace(@"\", @"\\");
        var content = $$"""
        "libraryfolders"
        {
            "1"
            {
                "path" "{{escaped}}"
            }
        }
        """;
        File.WriteAllText(Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"), content);
    }

    public void CreateInstallingGame(int appId, bool installed, string? libraryRoot = null)
    {
        var root = libraryRoot ?? SteamRoot;
        Directory.CreateDirectory(DownloadingPath(appId, root));
        File.WriteAllText(Path.Combine(DownloadingPath(appId, root), "placeholder.bin"), "partial");
        if (installed)
        {
            File.WriteAllText(ManifestPath(appId, root), $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "StateFlags" "4"
            }
            """);
        }
        else
        {
            File.WriteAllText(ManifestPath(appId, root), $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "StateFlags" "1026"
            }
            """);
        }
    }

    public string ManifestPath(int appId, string? libraryRoot = null)
    {
        return Path.Combine(libraryRoot ?? SteamRoot, "steamapps", $"appmanifest_{appId}.acf");
    }

    public string DownloadingPath(int appId, string? libraryRoot = null)
    {
        return Path.Combine(libraryRoot ?? SteamRoot, "steamapps", "downloading", appId.ToString());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
