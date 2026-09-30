using System.Text.Json;
using System.Text.Json.Serialization;
using ClipboardHistoryManager.Models;
using ClipboardHistoryManager.Infrastructure;

namespace ClipboardHistoryManager.Services;

public sealed class SettingsManager
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsManager(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? AppPaths.SettingsPath;
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        if (!File.Exists(SettingsPath))
        {
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            Current.Normalize();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Settings file could not be loaded. Defaults will be used.", ex);
            Current = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(Current, _jsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
