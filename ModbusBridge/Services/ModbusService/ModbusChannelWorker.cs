using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;
using ModbusBridge.Utilities;

namespace ModbusBridge.Services;

public sealed class ModbusChannelWorker
{
    private readonly ModbusChannelSettings _channelSettings;
    private readonly ModbusSettings _modbusSettings;
    private readonly RegisterCache _registerCache;
    private readonly IStatisticsService _statisticsService;
    private WorkerStatus _status;

    public ModbusChannelWorker(
        ModbusChannelSettings channelSettings,
        ModbusSettings modbusSettings,
        RegisterCache registerCache,
        IStatisticsService statisticsService)
    {
        _channelSettings = channelSettings;
        _modbusSettings = modbusSettings;
        _registerCache = registerCache;
        _statisticsService = statisticsService;
        _status = new WorkerStatus
        {
            Name = _channelSettings.Name,
            State = WorkerState.Stopped,
            Message = $"{_channelSettings.PortName} stopped."
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Logger.Log($"[MODBUS:{_channelSettings.Name}] Worker skeleton started on {_channelSettings.PortName}.");
        _status = new WorkerStatus
        {
            Name = _channelSettings.Name,
            State = WorkerState.Running,
            Message = $"{_channelSettings.PortName} ready. Polling logic is not implemented yet."
        };

        _ = _modbusSettings;
        _ = _registerCache;
        _ = _statisticsService;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Logger.Log($"[MODBUS:{_channelSettings.Name}] Worker skeleton stopped.");
        _status = new WorkerStatus
        {
            Name = _channelSettings.Name,
            State = WorkerState.Stopped,
            Message = $"{_channelSettings.PortName} stopped."
        };

        return Task.CompletedTask;
    }

    public WorkerStatus GetStatus()
    {
        return _status;
    }
}
