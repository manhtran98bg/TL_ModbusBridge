using System.Collections.Generic;

namespace ModbusBridge.Models;

public sealed class ApplicationSettings
{
    public SiemensSettings Siemens { get; set; } = new();
    public ModbusSettings Modbus { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
}

public sealed class SiemensSettings
{
    public string IpAddress { get; set; } = "192.168.1.10";
    public string CpuType { get; set; } = "S71200";
    public int Port { get; set; } = 102;
    public int Rack { get; set; }
    public int Slot { get; set; } = 1;
    public int WriteIntervalMs { get; set; } = 100;
    public bool AutoReconnect { get; set; } = true;
}

public sealed class ModbusSettings
{
    public int BaudRate { get; set; } = 38400;
    public string Parity { get; set; } = "Even";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public int RequestIntervalMs { get; set; } = 10;
    public int TimeoutMs { get; set; } = 100;
    public int RetryCount { get; set; } = 1;
    public string RegisterAddressMode { get; set; } = "Vendor4x";
    public List<int> Registers { get; set; } = [408502, 408503, 405202, 405213, 403202];
    public List<ModbusChannelSettings> Channels { get; set; } = [];
}

public sealed class ModbusChannelSettings
{
    public bool Enable { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public byte FirstSlaveId { get; set; }
    public byte LastSlaveId { get; set; }
    public int PlcMemoryStart { get; set; }
}

public sealed class UiSettings
{
    public int RefreshIntervalMs { get; set; } = 500;
}
