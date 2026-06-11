using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public sealed class ModbusService : IModbusService
{
    private readonly List<ModbusChannelWorker> _workers;

    public ModbusService(
        IOptionsMonitor<ApplicationSettings> options,
        RegisterCache registerCache,
        IStatisticsService statisticsService)
    {
        var settings = options.CurrentValue;
        _workers = settings.Modbus.Channels
            .Select(channel => new ModbusChannelWorker(channel, settings.Modbus, registerCache, statisticsService))
            .ToList();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
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
        }
    }

    public IReadOnlyList<WorkerStatus> GetWorkerStatuses()
    {
        return _workers.Select(worker => worker.GetStatus()).ToArray();
    }
}
