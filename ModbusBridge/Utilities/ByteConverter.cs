namespace ModbusBridge.Utilities;

public static class ByteConverter
{
    public static byte[] ToS7DWordBytes(uint value)
    {
        return
        [
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        ];
    }

    public static byte[] ToS7DWordBytes(ushort value)
    {
        return ToS7DWordBytes((uint)value);
    }
}
