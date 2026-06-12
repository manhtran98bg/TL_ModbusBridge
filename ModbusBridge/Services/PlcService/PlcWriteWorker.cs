using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;
using ModbusBridge.Utilities;
using S7.Net;

namespace ModbusBridge.Services;

public sealed class PlcWriteWorker : IDisposable
{
    private static readonly TimeSpan CounterResetInterval = TimeSpan.FromHours(1);
    private const int DefaultWriteIntervalMs = 1000;
    private const int DefaultReconnectDelayMs = 1000;
    private const int MaxReconnectDelayMs = 10000;

    private readonly object _syncRoot = new();
    private readonly object _counterResetSync = new();
    private readonly SiemensSettings _settings;
    private readonly RegisterCache _registerCache;
    private readonly IStatisticsService _statisticsService;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private Plc? _plc;
    private WorkerStatus _status;
    private long _writeCount;
    private long _errorCount;
    private long _counterWindowStartedAtTicks = DateTime.Now.Ticks;
    private long _lastWriteElapsedTicks;
    private long _lastSuccessTimestampTicks;
    private long _lastErrorTimestampTicks;
    private int _lastMemoryStart;
    private int _lastByteCount;
    private bool _disposed;

    public PlcWriteWorker(SiemensSettings settings, RegisterCache registerCache, IStatisticsService statisticsService)
    {
        _settings = settings;
        _registerCache = registerCache;
        _statisticsService = statisticsService;
        _status = new WorkerStatus
        {
            Kind = WorkerKind.Plc,
            Name = "PLC",
            PortName = _settings.IpAddress,
            State = WorkerState.Stopped,
            Message = "PLC writer stopped."
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            if (_runTask is { IsCompleted: false })
            {
                Logger.Log("[PLC] Start skipped: worker already running.");
                return Task.CompletedTask;
            }

            UpdateStatus(WorkerState.Starting, "PLC writer starting.");
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(() => RunAsync(_runCts.Token), CancellationToken.None);
        }

        Logger.Log($"[PLC] Writer started for {_settings.CpuType} {_settings.IpAddress}:{_settings.Port}. Write interval={NormalizeWriteIntervalMs()} ms.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? runTask;
        lock (_syncRoot)
        {
            UpdateStatus(WorkerState.Stopping, "PLC writer stopping.");
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

        CloseConnection();
        UpdateStatus(WorkerState.Stopped, "PLC writer stopped.");
        Logger.Log("[PLC] Write worker stopped.");
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
        CloseConnection();
        _runCts?.Dispose();
        _disposed = true;
    }

    private async Task RunAsync(CancellationToken token)
    {
        var reconnectDelayMs = DefaultReconnectDelayMs;

        while (!token.IsCancellationRequested)
        {
            var connected = false;
            try
            {
                UpdateStatus(WorkerState.Connecting, $"Connecting PLC {_settings.CpuType} {_settings.IpAddress}:{_settings.Port}.");
                await OpenConnectionAsync(token);
                connected = true;
                reconnectDelayMs = DefaultReconnectDelayMs;

                UpdateStatus(WorkerState.Running, $"PLC connected. Write interval={NormalizeWriteIntervalMs()} ms.");
                await WriteLoopAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                if (!connected)
                {
                    RegisterWriteAttempt(false, TimeSpan.Zero, 0, 0);
                }

                Logger.Log($"[PLC] Worker error: {exception.Message}");

                if (!_settings.AutoReconnect)
                {
                    UpdateStatus(WorkerState.Faulted, $"PLC faulted: {exception.Message}");
                    break;
                }

                UpdateStatus(WorkerState.Reconnecting, $"PLC reconnecting in {reconnectDelayMs} ms: {exception.Message}");
            }
            finally
            {
                CloseConnection();
            }

            if (token.IsCancellationRequested || !_settings.AutoReconnect)
            {
                break;
            }

            await Task.Delay(reconnectDelayMs, token);
            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, MaxReconnectDelayMs);
        }
    }

    private async Task OpenConnectionAsync(CancellationToken token)
    {
        CloseConnection();

        var cpuType = ParseCpuType(_settings.CpuType);
        var plc = new Plc(
            cpuType,
            _settings.IpAddress,
            _settings.Port,
            (short)_settings.Rack,
            (short)_settings.Slot);

        try
        {
            await plc.OpenAsync(token);
            if (!plc.IsConnected)
            {
                throw new IOException($"PLC {_settings.IpAddress}:{_settings.Port} open completed but connection is not active.");
            }

            _plc = plc;
        }
        catch
        {
            plc.Close();
            throw;
        }
    }

    private async Task WriteLoopAsync(CancellationToken token)
    {
        var intervalMs = NormalizeWriteIntervalMs();

        while (!token.IsCancellationRequested)
        {
            var cycleStopwatch = Stopwatch.StartNew();
            var writeStopwatch = Stopwatch.StartNew();
            var blocks = BuildWriteBlocks();

            if (blocks.Count == 0)
            {
                writeStopwatch.Stop();
                UpdateStatus(WorkerState.Running, "PLC connected. Waiting for valid Modbus values.");
                await DelayRemainingCycleAsync(cycleStopwatch.Elapsed, intervalMs, token);
                continue;
            }

            try
            {
                var plc = GetConnectedPlc();
                foreach (var block in blocks)
                {
                    token.ThrowIfCancellationRequested();
                    await plc.WriteBytesAsync(DataType.Memory, 0, block.MemoryStart, block.Buffer, token);
                }

                writeStopwatch.Stop();

                var firstAddress = blocks.Min(block => block.MemoryStart);
                var byteCount = blocks.Sum(block => block.Buffer.Length);
                RegisterWriteAttempt(true, writeStopwatch.Elapsed, firstAddress, byteCount);
                UpdateStatus(
                    WorkerState.Running,
                    $"PLC write OK. Blocks={blocks.Count}, bytes={byteCount}, elapsed={writeStopwatch.Elapsed.TotalMilliseconds:0.0} ms.");
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                writeStopwatch.Stop();
                var firstAddress = blocks.Min(block => block.MemoryStart);
                var byteCount = blocks.Sum(block => block.Buffer.Length);
                RegisterWriteAttempt(false, writeStopwatch.Elapsed, firstAddress, byteCount);
                UpdateStatus(WorkerState.Reconnecting, $"PLC write failed: {exception.Message}");
                throw;
            }

            await DelayRemainingCycleAsync(cycleStopwatch.Elapsed, intervalMs, token);
        }
    }

    private IReadOnlyList<PlcWriteBlock> BuildWriteBlocks()
    {
        var values = _registerCache.GetSnapshot()
            .Where(value => value.Quality == RegisterQuality.Good)
            .OrderBy(value => value.Request.PlcMemoryAddress)
            .ToArray();

        if (values.Length == 0)
        {
            return [];
        }

        var blocks = new List<PlcWriteBlock>();
        var currentStart = values[0].Request.PlcMemoryAddress;
        var currentBuffer = new List<byte>();

        foreach (var value in values)
        {
            var expectedAddress = currentStart + currentBuffer.Count;
            var currentAddress = value.Request.PlcMemoryAddress;
            var bytes = ByteConverter.ToS7DWordBytes(value.Value);

            if (currentBuffer.Count > 0 && currentAddress != expectedAddress)
            {
                blocks.Add(CreateWriteBlock(currentStart, currentBuffer));
                currentStart = currentAddress;
                currentBuffer = [];
            }

            currentBuffer.AddRange(bytes);
        }

        if (currentBuffer.Count > 0)
        {
            blocks.Add(CreateWriteBlock(currentStart, currentBuffer));
        }

        return blocks;
    }

    private static PlcWriteBlock CreateWriteBlock(int memoryStart, IReadOnlyCollection<byte> buffer)
    {
        return new PlcWriteBlock
        {
            Name = $"M{memoryStart}",
            MemoryStart = memoryStart,
            Buffer = buffer.ToArray(),
            Timestamp = DateTime.Now
        };
    }

    private Plc GetConnectedPlc()
    {
        if (_plc is null || !_plc.IsConnected)
        {
            throw new IOException($"PLC {_settings.IpAddress}:{_settings.Port} is not connected.");
        }

        return _plc;
    }

    private void CloseConnection()
    {
        try
        {
            _plc?.Close();
        }
        catch (Exception exception)
        {
            Logger.LogException(exception, "[PLC] CloseConnection");
        }
        finally
        {
            _plc = null;
        }
    }

    private void RegisterWriteAttempt(bool success, TimeSpan elapsed, int memoryStart, int byteCount)
    {
        ResetCountersIfWindowExpired(DateTime.Now);

        Interlocked.Increment(ref _writeCount);
        Interlocked.Exchange(ref _lastWriteElapsedTicks, elapsed.Ticks);
        Volatile.Write(ref _lastMemoryStart, memoryStart);
        Volatile.Write(ref _lastByteCount, byteCount);
        _statisticsService.RegisterPlcWrite(success, elapsed);

        if (success)
        {
            Interlocked.Exchange(ref _lastSuccessTimestampTicks, DateTime.Now.Ticks);
            return;
        }

        Interlocked.Increment(ref _errorCount);
        Interlocked.Exchange(ref _lastErrorTimestampTicks, DateTime.Now.Ticks);
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
            Kind = WorkerKind.Plc,
            Name = "PLC",
            PortName = $"{_settings.IpAddress}:{_settings.Port}",
            State = state,
            Message = message,
            Timestamp = DateTime.Now,
            RequestCount = Interlocked.Read(ref _writeCount),
            ErrorCount = Interlocked.Read(ref _errorCount),
            LastReadElapsed = TimeSpan.FromTicks(Interlocked.Read(ref _lastWriteElapsedTicks)),
            LastSuccessTimestamp = CreateTimestamp(Interlocked.Read(ref _lastSuccessTimestampTicks)),
            LastErrorTimestamp = CreateTimestamp(Interlocked.Read(ref _lastErrorTimestampTicks)),
            LastStartAddress = Volatile.Read(ref _lastMemoryStart),
            LastQuantity = Volatile.Read(ref _lastByteCount)
        };
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

            Interlocked.Exchange(ref _writeCount, 0);
            Interlocked.Exchange(ref _errorCount, 0);
            Interlocked.Exchange(ref _counterWindowStartedAtTicks, now.Ticks);
        }
    }

    private int NormalizeWriteIntervalMs()
    {
        return Math.Max(DefaultWriteIntervalMs, _settings.WriteIntervalMs);
    }

    private static async Task DelayRemainingCycleAsync(TimeSpan elapsed, int intervalMs, CancellationToken token)
    {
        var remainingMs = intervalMs - (int)elapsed.TotalMilliseconds;
        if (remainingMs > 0)
        {
            await Task.Delay(remainingMs, token);
        }
    }

    private static CpuType ParseCpuType(string value)
    {
        return Enum.TryParse<CpuType>(value, true, out var cpuType)
            ? cpuType
            : throw new InvalidOperationException($"Unsupported Siemens CpuType '{value}'.");
    }

    private static DateTime CreateTimestamp(long ticks)
    {
        return ticks <= 0 ? DateTime.MinValue : new DateTime(ticks);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlcWriteWorker));
        }
    }
}
