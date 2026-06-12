using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;
using ModbusBridge.Utilities;
using NModbus;
using NModbus.Logging;
using NModbus.Serial;

namespace ModbusBridge.Services;

public sealed class ModbusChannelWorker : IDisposable
{
    private static readonly TimeSpan CounterResetInterval = TimeSpan.FromHours(1);
    private readonly object _syncRoot = new();
    private readonly object _counterResetSync = new();
    private readonly ModbusChannelSettings _channelSettings;
    private readonly ModbusSettings _modbusSettings;
    private readonly RegisterCache _registerCache;
    private readonly IStatisticsService _statisticsService;
    private readonly IReadOnlyList<ModbusReadBatch> _readPlan;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private SerialPort? _serialPort;
    private IModbusMaster? _modbusMaster;
    private WorkerStatus _status;
    private long _requestCount;
    private long _errorCount;
    private long _counterWindowStartedAtTicks = DateTime.Now.Ticks;
    private long _lastReadElapsedTicks;
    private long _lastSuccessTimestampTicks;
    private long _lastErrorTimestampTicks;
    private byte _lastSlaveId;
    private ushort _lastStartAddress;
    private ushort _lastQuantity;
    private bool _disposed;

    public ModbusChannelWorker(
        ModbusChannelSettings channelSettings,
        ModbusSettings modbusSettings,
        RegisterCache registerCache,
        IStatisticsService statisticsService)
    {
        _channelSettings = channelSettings;
        _modbusSettings = modbusSettings;
        _registerCache = registerCache;
        _statisticsService = statisticsService;
        _readPlan = BuildReadPlan();
        _status = new WorkerStatus
        {
            Kind = WorkerKind.Modbus,
            Name = _channelSettings.Name,
            PortName = _channelSettings.PortName,
            State = WorkerState.Stopped,
            Message = $"{_channelSettings.PortName} stopped."
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            if (_runTask is { IsCompleted: false })
            {
                Logger.Log($"[MODBUS:{_channelSettings.Name}] Start skipped: worker already running.");
                return Task.CompletedTask;
            }

            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(() => RunAsync(_runCts.Token), CancellationToken.None);
        }

        Logger.Log($"[MODBUS:{_channelSettings.Name}] Worker started on {_channelSettings.PortName}. Read batches={_readPlan.Count}.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? runTask;
        lock (_syncRoot)
        {
            UpdateStatus(WorkerState.Stopping, $"{_channelSettings.PortName} stopping.");
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
        UpdateStatus(WorkerState.Stopped, $"{_channelSettings.PortName} stopped.");
        Logger.Log($"[MODBUS:{_channelSettings.Name}] Worker stopped.");
    }

    public WorkerStatus GetStatus()
    {
        return CreateStatus(_status.State, _status.Message);
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
        if (_readPlan.Count == 0)
        {
            UpdateStatus(WorkerState.Faulted, $"{_channelSettings.PortName} has no Modbus read plan.");
            Logger.Log($"[MODBUS:{_channelSettings.Name}] Worker stopped: no read plan. Check slave range and registers config.");
            return;
        }

        var reconnectDelayMs = NormalizeDelay(_modbusSettings.ReconnectDelayMs, 1000);
        var maxReconnectDelayMs = Math.Max(reconnectDelayMs, NormalizeDelay(_modbusSettings.MaxReconnectDelayMs, 10000));

        while (!token.IsCancellationRequested)
        {
            try
            {
                UpdateStatus(WorkerState.Connecting, $"Opening {_channelSettings.PortName}.");
                OpenBus();
                reconnectDelayMs = NormalizeDelay(_modbusSettings.ReconnectDelayMs, 1000);
                UpdateStatus(WorkerState.Running, $"{_channelSettings.PortName} running. Batches={_readPlan.Count}.");

                await PollLoopAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                //Logger.LogException(exception, $"[MODBUS:{_channelSettings.Name}] RunAsync");
                UpdateStatus(WorkerState.Reconnecting, $"{_channelSettings.PortName} reconnecting after error: {exception.Message}");
            }
            finally
            {
                CloseBus();
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(reconnectDelayMs, token);
            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);
        }
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        var consecutiveErrors = 0;
        var maxConsecutiveErrors = Math.Max(1, _modbusSettings.MaxConsecutiveErrors);

        while (!token.IsCancellationRequested)
        {
            foreach (var batch in _readPlan)
            {
                token.ThrowIfCancellationRequested();

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var master = GetConnectedMaster();
                    var values = master.ReadHoldingRegisters(batch.SlaveId, batch.StartAddress, batch.Quantity);
                    stopwatch.Stop();

                    if (values.Length < batch.Requests.Count)
                    {
                        throw new IOException($"Response length {values.Length} is shorter than expected {batch.Requests.Count}.");
                    }

                    for (var index = 0; index < batch.Requests.Count; index++)
                    {
                        _registerCache.Set(new ModbusRegisterValue
                        {
                            Request = batch.Requests[index],
                            Value = values[index],
                            Quality = RegisterQuality.Good,
                            Timestamp = DateTime.Now
                        });
                    }

                    _statisticsService.RegisterModbusRequest(true, stopwatch.Elapsed);
                    RegisterChannelRequest(batch, true, stopwatch.Elapsed);
                    consecutiveErrors = 0;
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    var busFault = IsBusFault(exception);
                    if (busFault)
                    {
                        consecutiveErrors++;
                    }

                    _statisticsService.RegisterModbusRequest(false, stopwatch.Elapsed);
                    RegisterChannelRequest(batch, false, stopwatch.Elapsed);
                    MarkBatchFailed(batch, exception);

                    if (_modbusSettings.LogReadErrors)
                    {
                        Logger.Log($"[MODBUS:{_channelSettings.Name}] Read failed on slave={batch.SlaveId}, address={batch.StartAddress}, qty={batch.Quantity}, consecutiveBusErrors={consecutiveErrors}: {exception.Message}");
                    }

                    if (busFault && consecutiveErrors >= maxConsecutiveErrors)
                    {
                        throw new IOException($"Modbus bus fault on {_channelSettings.PortName}. Consecutive errors={consecutiveErrors}.", exception);
                    }
                }

                await DelayBetweenRequestsAsync(token);
            }
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

        _serialPort = serialPort;
        _modbusMaster = factory.CreateMaster(transport);
    }

    private void CloseBus()
    {
        try
        {
            _modbusMaster?.Dispose();
        }
        catch (Exception exception)
        {
            Logger.LogException(exception, $"[MODBUS:{_channelSettings.Name}] Dispose master");
        }

        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }

            _serialPort?.Dispose();
        }
        catch (Exception exception)
        {
            Logger.LogException(exception, $"[MODBUS:{_channelSettings.Name}] Close serial port");
        }
        finally
        {
            _modbusMaster = null;
            _serialPort = null;
        }
    }

    private IModbusMaster GetConnectedMaster()
    {
        if (_serialPort?.IsOpen != true || _modbusMaster is null)
        {
            throw new IOException($"{_channelSettings.PortName} is not open.");
        }

        return _modbusMaster;
    }

    private IReadOnlyList<ModbusReadBatch> BuildReadPlan()
    {
        var batches = new List<ModbusReadBatch>();
        var registerCount = _modbusSettings.Registers.Count;

        if (registerCount == 0)
        {
            return batches;
        }

        for (var slaveId = _channelSettings.FirstSlaveId; slaveId <= _channelSettings.LastSlaveId; slaveId++)
        {
            var requests = _modbusSettings.Registers
                .Select((address, index) => new ModbusReadRequest
                {
                    ChannelName = _channelSettings.Name,
                    PortName = _channelSettings.PortName,
                    SlaveId = (byte)slaveId,
                    VendorAddress = address,
                    ProtocolAddress = ModbusAddressConverter.ToProtocolAddress(address, _modbusSettings.RegisterAddressMode),
                    RegisterIndex = index,
                    PlcMemoryAddress = PlcAddressCalculator.CalculateDWordAddress(
                        _channelSettings.PlcMemoryStart,
                        _channelSettings.FirstSlaveId,
                        (byte)slaveId,
                        registerCount,
                        index)
                })
                .ToArray();

            var currentBatchRequests = new List<ModbusReadRequest>();
            foreach (var request in requests)
            {
                if (currentBatchRequests.Count == 0)
                {
                    currentBatchRequests.Add(request);
                    continue;
                }

                var expectedNextAddress = currentBatchRequests[0].ProtocolAddress + currentBatchRequests.Count;
                if (request.ProtocolAddress == expectedNextAddress)
                {
                    currentBatchRequests.Add(request);
                    continue;
                }

                AddBatch(batches, currentBatchRequests);
                currentBatchRequests = [request];
            }

            AddBatch(batches, currentBatchRequests);
        }

        return batches;
    }

    private static void AddBatch(List<ModbusReadBatch> batches, IReadOnlyList<ModbusReadRequest> requests)
    {
        if (requests.Count == 0)
        {
            return;
        }

        batches.Add(new ModbusReadBatch
        {
            ChannelName = requests[0].ChannelName,
            PortName = requests[0].PortName,
            SlaveId = requests[0].SlaveId,
            StartAddress = requests[0].ProtocolAddress,
            Quantity = (ushort)requests.Count,
            Requests = requests.ToArray()
        });
    }

    private void MarkBatchFailed(ModbusReadBatch batch, Exception exception)
    {
        var quality = exception is TimeoutException ? RegisterQuality.Timeout : RegisterQuality.Bad;
        foreach (var request in batch.Requests)
        {
            _registerCache.SetError(request, quality, exception.Message);
        }
    }

    private async Task DelayBetweenRequestsAsync(CancellationToken token)
    {
        var delayMs = Math.Max(0, _modbusSettings.RequestIntervalMs);
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, token);
        }
    }

    private void UpdateStatus(WorkerState state, string message)
    {
        _status = CreateStatus(state, message);
    }

    private WorkerStatus CreateStatus(WorkerState state, string message)
    {
        ResetCountersIfWindowExpired(DateTime.Now);

        return new WorkerStatus
        {
            Kind = WorkerKind.Modbus,
            Name = _channelSettings.Name,
            PortName = _channelSettings.PortName,
            State = state,
            Message = message,
            Timestamp = DateTime.Now,
            RequestCount = Interlocked.Read(ref _requestCount),
            ErrorCount = Interlocked.Read(ref _errorCount),
            LastReadElapsed = TimeSpan.FromTicks(Interlocked.Read(ref _lastReadElapsedTicks)),
            LastSuccessTimestamp = CreateTimestamp(Interlocked.Read(ref _lastSuccessTimestampTicks)),
            LastErrorTimestamp = CreateTimestamp(Interlocked.Read(ref _lastErrorTimestampTicks)),
            LastSlaveId = _lastSlaveId,
            LastStartAddress = _lastStartAddress,
            LastQuantity = _lastQuantity
        };
    }

    private void RegisterChannelRequest(ModbusReadBatch batch, bool success, TimeSpan elapsed)
    {
        ResetCountersIfWindowExpired(DateTime.Now);

        Interlocked.Increment(ref _requestCount);
        Interlocked.Exchange(ref _lastReadElapsedTicks, elapsed.Ticks);
        _lastSlaveId = batch.SlaveId;
        _lastStartAddress = batch.StartAddress;
        _lastQuantity = batch.Quantity;

        if (success)
        {
            Interlocked.Exchange(ref _lastSuccessTimestampTicks, DateTime.Now.Ticks);
            return;
        }

        Interlocked.Increment(ref _errorCount);
        Interlocked.Exchange(ref _lastErrorTimestampTicks, DateTime.Now.Ticks);
    }

    private void ResetCountersIfWindowExpired(DateTime now)
    {
        var windowStartedAtTicks = Interlocked.Read(ref _counterWindowStartedAtTicks);
        if (now.Ticks - windowStartedAtTicks < CounterResetInterval.Ticks)
        {
            return;
        }

        lock (_counterResetSync)
        {
            windowStartedAtTicks = Interlocked.Read(ref _counterWindowStartedAtTicks);
            if (now.Ticks - windowStartedAtTicks < CounterResetInterval.Ticks)
            {
                return;
            }

            Interlocked.Exchange(ref _requestCount, 0);
            Interlocked.Exchange(ref _errorCount, 0);
            Interlocked.Exchange(ref _counterWindowStartedAtTicks, now.Ticks);
        }
    }

    private static DateTime CreateTimestamp(long ticks)
    {
        return ticks <= 0 ? DateTime.MinValue : new DateTime(ticks);
    }

    private static bool IsBusFault(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or TimeoutException;
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
            throw new ObjectDisposedException(nameof(ModbusChannelWorker));
        }
    }
}
