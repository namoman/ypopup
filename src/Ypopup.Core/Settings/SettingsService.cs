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
            NormalizeShareFolderSettings(settings);
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    private static void NormalizeShareFolderSettings(AppSettings settings)
    {
        if (!settings.ShareFolderEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.ShareFolderPath))
        {
            settings.ShareFolderPath = SharedFolderPathHelper.GetDefaultShareFolderPath();
            return;
        }

        var defaultPath = SharedFolderPathHelper.GetDefaultShareFolderPath();
        var isLegacyDocumentsPath = settings.ShareFolderPath.Contains(
            Path.Combine(AppConstants.AppFolderName, "공유폴더"),
            StringComparison.OrdinalIgnoreCase);

        if (isLegacyDocumentsPath && !Directory.Exists(settings.ShareFolderPath))
        {
            settings.ShareFolderPath = defaultPath;
        }
    }

    private static void EnsureDirectories(AppSettings settings)
    {
        Directory.CreateDirectory(settings.ReceiveDirectory);
        Directory.CreateDirectory(settings.ShareFolderPath);
    }
}
