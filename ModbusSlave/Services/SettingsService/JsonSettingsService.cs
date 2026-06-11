using System.Text.Json;
using ModbusSlave.Models;
using ModbusSlave.Utilities;

namespace ModbusSlave.Services;

public sealed class JsonSettingsService : ISettingsService
{
    public async Task<ApplicationSettings> GetSettingsAsync()
    {
        AppStoragePaths.EnsureConfigFile();

        var json = await File.ReadAllTextAsync(AppStoragePaths.SettingsPath);
        return JsonSerializer.Deserialize<ApplicationSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new ApplicationSettings();
    }
}
