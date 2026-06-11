using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ModbusBridge.Models;

public sealed class RegisterCache
{
    private readonly ConcurrentDictionary<string, ModbusRegisterValue> _values = new();

    public void Set(ModbusRegisterValue value)
    {
        _values[BuildKey(value.Request)] = value;
    }

    public ModbusRegisterValue? Get(ModbusReadRequest request)
    {
        return _values.TryGetValue(BuildKey(request), out var value) ? value : null;
    }

    public void SetError(ModbusReadRequest request, RegisterQuality quality, string errorMessage)
    {
        var current = Get(request);
        Set(new ModbusRegisterValue
        {
            Request = request,
            Value = current?.Value ?? 0,
            Quality = quality,
            Timestamp = DateTime.Now,
            ErrorMessage = errorMessage
        });
    }

    public IReadOnlyList<ModbusRegisterValue> GetSnapshot()
    {
        return _values.Values
            .OrderBy(value => value.Request.PlcMemoryAddress)
            .ToArray();
    }

    private static string BuildKey(ModbusReadRequest request)
    {
        return $"{request.ChannelName}:{request.SlaveId}:{request.VendorAddress}";
    }
}
