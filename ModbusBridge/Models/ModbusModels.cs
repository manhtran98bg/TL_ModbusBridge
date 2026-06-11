using System;
using System.Collections.Generic;

namespace ModbusBridge.Models;

public enum RegisterQuality
{
    Unknown,
    Good,
    Bad,
    Timeout
}

public sealed class ModbusReadRequest
{
    public string ChannelName { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public byte SlaveId { get; init; }
    public int VendorAddress { get; init; }
    public ushort ProtocolAddress { get; init; }
    public int RegisterIndex { get; init; }
    public int PlcMemoryAddress { get; init; }
}

public sealed class ModbusReadBatch
{
    public string ChannelName { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public byte SlaveId { get; init; }
    public ushort StartAddress { get; init; }
    public ushort Quantity { get; init; }
    public IReadOnlyList<ModbusReadRequest> Requests { get; init; } = [];
}

public sealed class ModbusRegisterValue
{
    public ModbusReadRequest Request { get; init; } = new();
    public ushort Value { get; init; }
    public RegisterQuality Quality { get; init; } = RegisterQuality.Unknown;
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string? ErrorMessage { get; init; }
}

public sealed class ModbusChannelSnapshot
{
    public string ChannelName { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public bool IsRunning { get; init; }
    public long RequestCount { get; init; }
    public long ErrorCount { get; init; }
    public DateTime LastUpdated { get; init; } = DateTime.MinValue;
}
