using ModbusSlave.Models;

namespace ModbusSlave.Services;

public interface ISettingsService
{
    Task<ApplicationSettings> GetSettingsAsync();
}
