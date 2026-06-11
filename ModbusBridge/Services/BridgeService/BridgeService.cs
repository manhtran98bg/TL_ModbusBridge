using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;
using ModbusBridge.Utilities;

namespace ModbusBridge.Services;

public sealed class BridgeService : IBridgeService
{
    private readonly IModbusService _modbusService;
    private readonly IPlcService _plcService;
    private readonly IStatisticsService _statisticsService;
    private bool _isRunning;

    public BridgeService(IModbusService modbusService, IPlcService plcService, IStatisticsService statisticsService)
    {
        _modbusService = modbusService;
        _plcService = plcService;
        _statisticsService = statisticsService;
    }

    public event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            RaiseStatus(true, "Bridge already running.");
            return;
        }

        Logger.Log("[BRIDGE] Starting workers.");
        await _modbusService.StartAsync(cancellationToken);
        await _plcService.StartAsync(cancellationToken);
        _isRunning = true;
        RaiseStatus(true, "Bridge running.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        Logger.Log("[BRIDGE] Stopping workers.");
        await _plcService.StopAsync(cancellationToken);
        await _modbusService.StopAsync(cancellationToken);
        _isRunning = false;
        RaiseStatus(false, "Bridge stopped.");
    }

    public BridgeSnapshot GetSnapshot()
    {
        var workers = _modbusService.GetWorkerStatuses()
            .Concat(_plcService.GetWorkerStatuses())
            .ToArray();

        return new BridgeSnapshot
        {
            Workers = workers,
            Statistics = _statisticsService.GetSnapshot()
        };
    }

    private void RaiseStatus(bool isRunning, string message)
    {
        StatusChanged?.Invoke(this, new BridgeStatusChangedEventArgs
        {
            IsRunning = isRunning,
            Message = message
        });
    }
}
