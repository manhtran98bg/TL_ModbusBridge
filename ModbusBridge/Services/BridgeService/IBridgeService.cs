using System;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public interface IBridgeService
{
    event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    BridgeSnapshot GetSnapshot();
}
