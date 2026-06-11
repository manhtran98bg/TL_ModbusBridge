using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public interface IPlcService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<WorkerStatus> GetWorkerStatuses();
}
