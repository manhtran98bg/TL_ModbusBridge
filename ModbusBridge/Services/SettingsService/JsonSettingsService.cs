using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModbusBridge.Models;
using ModbusBridge.Utilities;

namespace ModbusBridge.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<ApplicationSettings> GetSettingsAsync()
    {
        AppStoragePaths.EnsureConfigFile();
        var json = await File.ReadAllTextAsync(AppStoragePaths.SettingsPath);
        return JsonSerializer.Deserialize<ApplicationSettings>(json) ?? new ApplicationSettings();
    }

    public async Task SetSettingsAsync(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppStoragePaths.SettingsPath)!);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(AppStoragePaths.SettingsPath, json);
    }
}
