using System;
using System.Collections.Generic;

namespace ModbusBridge.Models;

public enum WorkerState
{
    Stopped,
    Starting,
    Connecting,
    Running,
    Reconnecting,
    Stopping,
    Faulted
}

public enum WorkerKind
{
    Modbus,
    Plc
}

public sealed class DriveRegisterMapping
{
    public string ChannelName { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public byte SlaveId { get; init; }
    public int RegisterIndex { get; init; }
    public int VendorAddress { get; init; }
    public ushort ProtocolAddress { get; init; }
    public int PlcMemoryAddress { get; init; }
}

public sealed class WorkerStatus
{
    public WorkerKind Kind { get; init; } = WorkerKind.Modbus;
    public string Name { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public WorkerState State { get; init; } = WorkerState.Stopped;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public long RequestCount { get; init; }
    public long ErrorCount { get; init; }
    public TimeSpan LastReadElapsed { get; init; }
    public DateTime LastSuccessTimestamp { get; init; } = DateTime.MinValue;
    public DateTime LastErrorTimestamp { get; init; } = DateTime.MinValue;
    public byte LastSlaveId { get; init; }
    public ushort LastStartAddress { get; init; }
    public ushort LastQuantity { get; init; }
}

public sealed class BridgeStatistics
{
    public long ModbusRequestCount { get; init; }
    public long ModbusErrorCount { get; init; }
    public long PlcWriteCount { get; init; }
    public long PlcErrorCount { get; init; }
    public TimeSpan LastModbusElapsed { get; init; }
    public TimeSpan LastPlcWriteElapsed { get; init; }
}

public sealed class BridgeSnapshot
{
    public IReadOnlyList<WorkerStatus> Workers { get; init; } = [];
    public BridgeStatistics Statistics { get; init; } = new();
}

public sealed class BridgeStatusChangedEventArgs : EventArgs
{
    public bool IsRunning { get; init; }
    public string Message { get; init; } = string.Empty;
}
