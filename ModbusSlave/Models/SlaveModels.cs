namespace ModbusSlave.Models;

public sealed class SlaveRegisterDefinition
{
    public int VendorAddress { get; init; }
    public ushort ProtocolAddress { get; init; }
    public int RegisterIndex { get; init; }
}

public sealed class ChannelSlaveDefinition
{
    public string ChannelName { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public byte SlaveId { get; init; }
    public IReadOnlyList<SlaveRegisterDefinition> Registers { get; init; } = [];
}
