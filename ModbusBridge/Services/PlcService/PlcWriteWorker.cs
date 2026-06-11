using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;
using ModbusBridge.Utilities;

namespace ModbusBridge.Services;

public sealed class PlcWriteWorker
{
    private readonly SiemensSettings _settings;
    private readonly RegisterCache _registerCache;
    private readonly IStatisticsService _statisticsService;

    public PlcWriteWorker(SiemensSettings settings, RegisterCache registerCache, IStatisticsService statisticsService)
    {
        _settings = settings;
        _registerCache = registerCache;
        _statisticsService = statisticsService;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Logger.Log($"[PLC] Write interval configured at {_settings.WriteIntervalMs} ms.");
        _ = _registerCache;
        _ = _statisticsService;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Logger.Log("[PLC] Write worker stopped.");
        return Task.CompletedTask;
    }
}
