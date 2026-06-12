using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public sealed class ModbusService : IModbusService
{
    private readonly ISettingsService _settingsService;
    private readonly RegisterCache _registerCache;
    private readonly IStatisticsService _statisticsService;
    private List<ModbusChannelWorker> _workers = [];

    public ModbusService(
        ISettingsService settingsService,
        RegisterCache registerCache,
        IStatisticsService statisticsService)
    {
        _settingsService = settingsService;
        _registerCache = registerCache;
        _statisticsService = statisticsService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _workers = settings.Modbus.Channels
            .Where(channel => channel.Enable)
            .Select(channel => new ModbusChannelWorker(channel, settings.Modbus, _registerCache, _statisticsService))
            .ToList();

        foreach (var worker in _workers)
        {
            await worker.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var worker in _workers)
        {
            await worker.StopAsync(cancellationToken);
            worker.Dispose();
        }

        _workers = [];
    }

    public IReadOnlyList<WorkerStatus> GetWorkerStatuses()
    {
        return _workers.Select(worker => worker.GetStatus()).ToArray();
    }
}
