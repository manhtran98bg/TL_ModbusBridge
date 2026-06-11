using System;

namespace ModbusBridge.Utilities;

public static class ModbusAddressConverter
{
    public static ushort ToProtocolAddress(int configuredAddress, string addressMode)
    {
        var protocolAddress = addressMode.Equals("Vendor4x", StringComparison.OrdinalIgnoreCase)
            ? configuredAddress - 400001
            : configuredAddress;

        if (protocolAddress < 0 || protocolAddress > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuredAddress),
                configuredAddress,
                "Modbus register address is outside the valid protocol range.");
        }

        return (ushort)protocolAddress;
    }
}
