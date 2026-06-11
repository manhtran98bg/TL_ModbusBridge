using System.IO.Ports;
using ModbusSlave.Models;
using ModbusSlave.Utilities;
using NModbus;
using NModbus.Data;
using NModbus.Logging;
using NModbus.Serial;

namespace ModbusSlave.Services;

public sealed class ModbusSlaveChannelWorker : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly ModbusChannelSettings _channelSettings;
    private readonly ModbusSettings _modbusSettings;
    private readonly IReadOnlyList<ChannelSlaveDefinition> _slaveDefinitions;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private SerialPort? _serialPort;
    private IModbusSlaveNetwork? _slaveNetwork;
    private bool _disposed;

    public ModbusSlaveChannelWorker(ModbusChannelSettings channelSettings, ModbusSettings modbusSettings)
    {
        _channelSettings = channelSettings;
        _modbusSettings = modbusSettings;
        _slaveDefinitions = BuildSlaveDefinitions();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            if (_runTask is { IsCompleted: false })
            {
                Logger.Log($"[SLAVE:{_channelSettings.Name}] Start skipped: worker already running.");
                return Task.CompletedTask;
            }

            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(() => RunAsync(_runCts.Token), CancellationToken.None);
        }

        Logger.Log($"[SLAVE:{_channelSettings.Name}] Worker started on {_channelSettings.PortName}. Slaves={_slaveDefinitions.Count}.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? runTask;
        lock (_syncRoot)
        {
            _runCts?.Cancel();
            runTask = _runTask;
        }

        if (runTask is not null)
        {
            try
            {
                await runTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        CloseBus();
        Logger.Log($"[SLAVE:{_channelSettings.Name}] Worker stopped.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _runCts?.Cancel();
        CloseBus();
        _runCts?.Dispose();
        _disposed = true;
    }

    private async Task RunAsync(CancellationToken token)
    {
        if (_slaveDefinitions.Count == 0)
        {
            Logger.Log($"[SLAVE:{_channelSettings.Name}] No slave definitions. Check config.");
            return;
        }

        var reconnectDelayMs = NormalizeDelay(_modbusSettings.ReconnectDelayMs, 1000);
        var maxReconnectDelayMs = Math.Max(reconnectDelayMs, NormalizeDelay(_modbusSettings.MaxReconnectDelayMs, 10000));

        while (!token.IsCancellationRequested)
        {
            try
            {
                OpenBus();
                reconnectDelayMs = NormalizeDelay(_modbusSettings.ReconnectDelayMs, 1000);
                Logger.Log($"[SLAVE:{_channelSettings.Name}] Listening on {_channelSettings.PortName}, ID {_channelSettings.FirstSlaveId}-{_channelSettings.LastSlaveId}.");
                await _slaveNetwork!.ListenAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Logger.LogException(exception, $"[SLAVE:{_channelSettings.Name}] RunAsync");
            }
            finally
            {
                CloseBus();
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            Logger.Log($"[SLAVE:{_channelSettings.Name}] Reconnecting in {reconnectDelayMs} ms.");
            await Task.Delay(reconnectDelayMs, token);
            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);
        }
    }

    private void OpenBus()
    {
        CloseBus();

        var serialPort = new SerialPort(
            _channelSettings.PortName,
            _modbusSettings.BaudRate,
            ParseEnum(_modbusSettings.Parity, Parity.Even),
            _modbusSettings.DataBits,
            ParseEnum(_modbusSettings.StopBits, StopBits.One))
        {
            ReadTimeout = Math.Max(1, _modbusSettings.TimeoutMs),
            WriteTimeout = Math.Max(1, _modbusSettings.TimeoutMs)
        };

        serialPort.Open();

        var factory = new ModbusFactory(
            Enumerable.Empty<IModbusFunctionService>(),
            includeBuiltIn: true,
            logger: NullModbusLogger.Instance);

        var adapter = new SerialPortAdapter(serialPort);
        var transport = factory.CreateRtuTransport(adapter);
        transport.ReadTimeout = Math.Max(1, _modbusSettings.TimeoutMs);
        transport.WriteTimeout = Math.Max(1, _modbusSettings.TimeoutMs);
        transport.Retries = Math.Max(0, _modbusSettings.RetryCount);
        transport.WaitToRetryMilliseconds = Math.Max(1, _modbusSettings.RequestIntervalMs);
        transport.SlaveBusyUsesRetryCount = true;

        var slaveNetwork = factory.CreateSlaveNetwork(transport);
        foreach (var definition in _slaveDefinitions)
        {
            slaveNetwork.AddSlave(factory.CreateSlave(definition.SlaveId, CreateDataStore(definition)));
        }

        _serialPort = serialPort;
        _slaveNetwork = slaveNetwork;
    }

    private void CloseBus()
    {
        try
        {
            _slaveNetwork = null;

            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }

            _serialPort?.Dispose();
        }
        catch (Exception exception)
        {
            Logger.LogException(exception, $"[SLAVE:{_channelSettings.Name}] CloseBus");
        }
        finally
        {
            _serialPort = null;
        }
    }

    private IReadOnlyList<ChannelSlaveDefinition> BuildSlaveDefinitions()
    {
        var registers = _modbusSettings.Registers
            .Select((address, index) => new SlaveRegisterDefinition
            {
                VendorAddress = address,
                ProtocolAddress = ModbusAddressConverter.ToProtocolAddress(address, _modbusSettings.RegisterAddressMode),
                RegisterIndex = index
            })
            .ToArray();

        var definitions = new List<ChannelSlaveDefinition>();
        for (var slaveId = _channelSettings.FirstSlaveId; slaveId <= _channelSettings.LastSlaveId; slaveId++)
        {
            definitions.Add(new ChannelSlaveDefinition
            {
                ChannelName = _channelSettings.Name,
                PortName = _channelSettings.PortName,
                SlaveId = (byte)slaveId,
                Registers = registers
            });
        }

        return definitions;
    }

    private static ISlaveDataStore CreateDataStore(ChannelSlaveDefinition definition)
    {
        var dataStore = new SlaveDataStore();

        foreach (var register in definition.Registers)
        {
            var value = CreateRegisterValue(definition.SlaveId, register);
            dataStore.HoldingRegisters.WritePoints(register.ProtocolAddress, [value]);
        }

        return dataStore;
    }

    private static ushort CreateRegisterValue(byte slaveId, SlaveRegisterDefinition register)
    {
        return (ushort)(slaveId * 100 + register.RegisterIndex + 1);
    }

    private static int NormalizeDelay(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed)
            ? parsed
            : fallback;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ModbusSlaveChannelWorker));
        }
    }
}
