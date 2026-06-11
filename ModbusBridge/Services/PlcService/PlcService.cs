using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ModbusBridge.Models;
using ModbusBridge.Utilities;

namespace ModbusBridge.Services;

public sealed class PlcService : IPlcService
{
    private readonly SiemensSettings _settings;
    private readonly PlcWriteWorker _writeWorker;
    private WorkerStatus _status = new()
    {
        Kind = WorkerKind.Plc,
        Name = "PLC",
        State = WorkerState.Stopped,
        Message = "PLC writer stopped."
    };

    public PlcService(
        IOptionsMonitor<ApplicationSettings> options,
        RegisterCache registerCache,
        IStatisticsService statisticsService)
    {
        _settings = options.CurrentValue.Siemens;
        _writeWorker = new PlcWriteWorker(_settings, registerCache, statisticsService);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Logger.Log($"[PLC] Writer skeleton started for {_settings.CpuType} {_settings.IpAddress}:{_settings.Port}.");
        _status = new WorkerStatus
        {
            Kind = WorkerKind.Plc,
            Name = "PLC",
            State = WorkerState.Running,
            Message = "PLC writer ready. S7 write logic is not implemented yet."
        };

        await _writeWorker.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _writeWorker.StopAsync(cancellationToken);
        _status = new WorkerStatus
        {
            Kind = WorkerKind.Plc,
            Name = "PLC",
            State = WorkerState.Stopped,
            Message = "PLC writer stopped."
        };
    }

    public IReadOnlyList<WorkerStatus> GetWorkerStatuses()
    {
        return [_status];
    }
}
