using System;
using System.Threading;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public sealed class StatisticsService : IStatisticsService
{
    private long _modbusRequestCount;
    private long _modbusErrorCount;
    private long _plcWriteCount;
    private long _plcErrorCount;
    private long _lastModbusElapsedTicks;
    private long _lastPlcWriteElapsedTicks;

    public BridgeStatistics GetSnapshot()
    {
        return new BridgeStatistics
        {
            ModbusRequestCount = Interlocked.Read(ref _modbusRequestCount),
            ModbusErrorCount = Interlocked.Read(ref _modbusErrorCount),
            PlcWriteCount = Interlocked.Read(ref _plcWriteCount),
            PlcErrorCount = Interlocked.Read(ref _plcErrorCount),
            LastModbusElapsed = TimeSpan.FromTicks(Interlocked.Read(ref _lastModbusElapsedTicks)),
            LastPlcWriteElapsed = TimeSpan.FromTicks(Interlocked.Read(ref _lastPlcWriteElapsedTicks))
        };
    }

    public void RegisterModbusRequest(bool success, TimeSpan elapsed)
    {
        Interlocked.Increment(ref _modbusRequestCount);
        Interlocked.Exchange(ref _lastModbusElapsedTicks, elapsed.Ticks);

        if (!success)
        {
            Interlocked.Increment(ref _modbusErrorCount);
        }
    }

    public void RegisterPlcWrite(bool success, TimeSpan elapsed)
    {
        Interlocked.Increment(ref _plcWriteCount);
        Interlocked.Exchange(ref _lastPlcWriteElapsedTicks, elapsed.Ticks);

        if (!success)
        {
            Interlocked.Increment(ref _plcErrorCount);
        }
    }
}
