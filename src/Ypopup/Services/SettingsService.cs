using System.Text.Json;
using Ypopup.Models;

namespace Ypopup.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Ypopup");
        Directory.CreateDirectory(appData);
        _settingsPath = Path.Combine(appData, "settings.json");
        Current = Load();
        Directory.CreateDirectory(Current.ReceiveDirectory);
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        Directory.CreateDirectory(Current.ReceiveDirectory);
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
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }
}
