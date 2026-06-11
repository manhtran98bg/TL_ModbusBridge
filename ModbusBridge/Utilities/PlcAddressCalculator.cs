namespace ModbusBridge.Utilities;

public static class PlcAddressCalculator
{
    public const int DWordSizeBytes = 4;

    public static int CalculateDWordAddress(
        int plcMemoryStart,
        byte firstSlaveId,
        byte slaveId,
        int registerCount,
        int registerIndex)
    {
        var driveIndex = slaveId - firstSlaveId;
        return plcMemoryStart + ((driveIndex * registerCount) + registerIndex) * DWordSizeBytes;
    }
}
