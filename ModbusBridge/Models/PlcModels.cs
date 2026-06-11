using System;

namespace ModbusBridge.Models;

public sealed class PlcWriteBlock
{
    public string Name { get; init; } = string.Empty;
    public int MemoryStart { get; init; }
    public byte[] Buffer { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public sealed class PlcConnectionStatus
{
    public bool IsConnected { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
