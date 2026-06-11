using System.Threading.Tasks;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public interface ISettingsService
{
    Task<ApplicationSettings> GetSettingsAsync();
    Task SetSettingsAsync(ApplicationSettings settings);
}
