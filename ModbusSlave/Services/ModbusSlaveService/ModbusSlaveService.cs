using ModbusSlave.Models;
using ModbusSlave.Utilities;

namespace ModbusSlave.Services;

public sealed class ModbusSlaveService : IDisposable
{
    private readonly List<ModbusSlaveChannelWorker> _workers;

    public ModbusSlaveService(ApplicationSettings settings)
    {
        _workers = settings.Modbus.Channels
            .Where(channel => channel.Enable)
            .Select(channel => new ModbusSlaveChannelWorker(channel, settings.Modbus))
            .ToList();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_workers.Count == 0)
        {
            Logger.Log("[SLAVE] No enabled Modbus channels in config.");
            return;
        }

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

    public void Dispose()
    {
        foreach (var worker in _workers)
        {
            worker.Dispose();
        }
    }
}
