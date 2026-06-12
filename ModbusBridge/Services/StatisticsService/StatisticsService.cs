using System;
using System.Threading;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public sealed class StatisticsService : IStatisticsService
{
    private static readonly TimeSpan CounterResetInterval = TimeSpan.FromHours(1);
    private readonly object _counterResetSync = new();
    private long _modbusRequestCount;
    private long _modbusErrorCount;
    private long _plcWriteCount;
    private long _plcErrorCount;
    private long _lastModbusElapsedTicks;
    private long _lastPlcWriteElapsedTicks;
    private long _counterWindowStartedAtTicks = DateTime.Now.Ticks;

    public BridgeStatistics GetSnapshot()
    {
        ResetCountersIfWindowExpired(DateTime.Now);

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
        ResetCountersIfWindowExpired(DateTime.Now);

        Interlocked.Increment(ref _modbusRequestCount);
        Interlocked.Exchange(ref _lastModbusElapsedTicks, elapsed.Ticks);

        if (!success)
        {
            Interlocked.Increment(ref _modbusErrorCount);
        }
    }

    public void RegisterPlcWrite(bool success, TimeSpan elapsed)
    {
        ResetCountersIfWindowExpired(DateTime.Now);

        Interlocked.Increment(ref _plcWriteCount);
        Interlocked.Exchange(ref _lastPlcWriteElapsedTicks, elapsed.Ticks);

        if (!success)
        {
            Interlocked.Increment(ref _plcErrorCount);
        }
    }

    private void ResetCountersIfWindowExpired(DateTime now)
    {
        var windowStartedAtTicks = Interlocked.Read(ref _counterWindowStartedAtTicks);
        if (now.Ticks - windowStartedAtTicks < CounterResetInterval.Ticks)
        {
            return;
        }

        lock (_counterResetSync)
        {
            windowStartedAtTicks = Interlocked.Read(ref _counterWindowStartedAtTicks);
            if (now.Ticks - windowStartedAtTicks < CounterResetInterval.Ticks)
            {
                return;
            }

            Interlocked.Exchange(ref _modbusRequestCount, 0);
            Interlocked.Exchange(ref _modbusErrorCount, 0);
            Interlocked.Exchange(ref _plcWriteCount, 0);
            Interlocked.Exchange(ref _plcErrorCount, 0);
            Interlocked.Exchange(ref _counterWindowStartedAtTicks, now.Ticks);
        }
    }
}
