using System.Text.Json;
using Ypopup.Core.Models;
using Ypopup.Core.Sharing;

namespace Ypopup.Core.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.AppFolderName);
        Directory.CreateDirectory(appData);
        _settingsPath = Path.Combine(appData, "settings.json");
        Current = Load();
        EnsureDirectories(Current);
    }

    public void Save(AppSettings settings)
    {
        NormalizeSettings(settings);
        Current = settings;
        EnsureDirectories(Current);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (NormalizeSettings(settings))
            {
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
            }

            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    private static bool NormalizeSettings(AppSettings settings)
    {
        var changed = false;
        changed |= NormalizeReceiveDirectorySettings(settings);
        changed |= NormalizeShareFolderSettings(settings);
        return changed;
    }

    private static bool NormalizeReceiveDirectorySettings(AppSettings settings)
    {
        var defaultPath = SharedFolderPathHelper.GetDefaultReceiveDirectory();

        if (string.IsNullOrWhiteSpace(settings.ReceiveDirectory))
        {
            settings.ReceiveDirectory = defaultPath;
            return true;
        }

        if (!IsLegacyReceiveDirectory(settings.ReceiveDirectory))
        {
            return false;
        }

        settings.ReceiveDirectory = defaultPath;
        return true;
    }

    private static bool IsLegacyReceiveDirectory(string path)
    {
        return path.Contains(
            Path.Combine(AppConstants.AppFolderName, "Received"),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool NormalizeShareFolderSettings(AppSettings settings)
    {
        var defaultPath = SharedFolderPathHelper.GetDefaultShareFolderPath();
        var changed = false;

        if (string.IsNullOrWhiteSpace(settings.ShareFolderPath)
            || IsLegacyShareFolderPath(settings.ShareFolderPath))
        {
            settings.ShareFolderPath = defaultPath;
            changed = true;
        }
        else
        {
            settings.ShareFolderPath = Path.GetFullPath(settings.ShareFolderPath);
        }

        if (!Directory.Exists(settings.ShareFolderPath))
        {
            settings.ShareFolderPath = defaultPath;
            changed = true;
        }

        return changed;
    }

    private static bool IsLegacyShareFolderPath(string path)
    {
        if (path.Contains(
                Path.Combine(AppConstants.AppFolderName, "공유폴더"),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Contains($"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}share", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/publish/share", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureDirectories(AppSettings settings)
    {
        Directory.CreateDirectory(settings.ReceiveDirectory);
        Directory.CreateDirectory(settings.ShareFolderPath);
    }
}
