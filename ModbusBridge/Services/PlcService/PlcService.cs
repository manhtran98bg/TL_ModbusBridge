using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public sealed class PlcService : IPlcService
{
    private readonly ISettingsService _settingsService;
    private readonly RegisterCache _registerCache;
    private readonly IStatisticsService _statisticsService;
    private PlcWriteWorker? _writeWorker;

    public PlcService(
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
        _writeWorker = new PlcWriteWorker(settings.Siemens, _registerCache, _statisticsService);
        await _writeWorker.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_writeWorker is null)
        {
            return;
        }

        await _writeWorker.StopAsync(cancellationToken);
        _writeWorker.Dispose();
        _writeWorker = null;
    }

    public IReadOnlyList<WorkerStatus> GetWorkerStatuses()
    {
        return [_writeWorker?.GetStatus() ?? new WorkerStatus
        {
            Kind = WorkerKind.Plc,
            Name = "PLC",
            State = WorkerState.Stopped,
            Message = "PLC writer stopped."
        }];
    }
}
