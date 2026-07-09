using Xunit;
using Ypopup.Core.Models;
using Ypopup.Core.Settings;
using Ypopup.Core.Sharing;

namespace Ypopup.Core.Tests.Settings;

public class SettingsMigrationTests
{
    [Fact]
    public void Constructor_BlankReceiveFolder_DefaultsToExeDown()
    {
        using var temp = new TempSettingsDir();
        File.WriteAllText(temp.SettingsPath, """
            {
              "DisplayName": "user",
              "ReceiveDirectory": ""
            }
            """);

        var service = new SettingsService(temp.Path);

        var expected = SharedFolderPathHelper.GetDefaultReceiveDirectory();
        Assert.Equal(NormalizeDir(expected), NormalizeDir(service.Current.ReceiveDirectory));
    }

    [Fact]
    public void Constructor_MigratesLegacyReceivePathToExeDown()
    {
        using var temp = new TempSettingsDir();
        File.WriteAllText(temp.SettingsPath, """
            {
              "DisplayName": "user",
              "ReceiveDirectory": "C:\\Users\\someone\\Documents\\Y-popup\\Received"
            }
            """);

        var service = new SettingsService(temp.Path);

        var expected = SharedFolderPathHelper.GetDefaultReceiveDirectory();
        Assert.Equal(NormalizeDir(expected), NormalizeDir(service.Current.ReceiveDirectory));
    }

    [Fact]
    public void Constructor_MigratesLegacySharePath_YpopupFolder()
    {
        using var temp = new TempSettingsDir();
        File.WriteAllText(temp.SettingsPath, """
            {
              "DisplayName": "user",
              "ShareFolderPath": "C:\\Users\\someone\\Documents\\Y-popup\\\uACF5\uC720\uD3F4\uB354"
            }
            """);

        var service = new SettingsService(temp.Path);

        var expected = SharedFolderPathHelper.GetDefaultShareFolderPath();
        Assert.Equal(NormalizeDir(expected), NormalizeDir(service.Current.ShareFolderPath));
    }

    [Fact]
    public void Constructor_MigratesLegacySharePath_PublishShare()
    {
        using var temp = new TempSettingsDir();
        File.WriteAllText(temp.SettingsPath, """
            {
              "DisplayName": "user",
              "ShareFolderPath": "C:\\dev\\publish\\share"
            }
            """);

        var service = new SettingsService(temp.Path);

        var expected = SharedFolderPathHelper.GetDefaultShareFolderPath();
        Assert.Equal(NormalizeDir(expected), NormalizeDir(service.Current.ShareFolderPath));
    }

    [Fact]
    public void Constructor_MissingSharePath_CreatedAndDefaults()
    {
        using var temp = new TempSettingsDir();
        File.WriteAllText(temp.SettingsPath, """
            { "DisplayName": "user" }
            """);

        var service = new SettingsService(temp.Path);

        Assert.True(Directory.Exists(service.Current.ShareFolderPath));
        Assert.True(Directory.Exists(service.Current.ReceiveDirectory));
    }

    [Fact]
    public void Save_PersistsNormalizedSettingsRoundTrip()
    {
        using var temp = new TempSettingsDir();
        var service = new SettingsService(temp.Path);

        var updated = service.Current;
        updated.DisplayName = "saved";

        service.Save(updated);

        var reloaded = new SettingsService(temp.Path);
        Assert.Equal("saved", reloaded.Current.DisplayName);
        Assert.True(Directory.Exists(reloaded.Current.ReceiveDirectory));
    }

    private static string NormalizeDir(string p) =>
        Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private sealed class TempSettingsDir : IDisposable
    {
        public string Path { get; }
        public string SettingsPath => System.IO.Path.Combine(Path, "settings.json");

        public TempSettingsDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ypopup-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}