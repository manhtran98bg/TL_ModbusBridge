using ModbusSlave.Services;
using ModbusSlave.Utilities;

Logger.Log("[APP] Hitachi Modbus Slave simulator starting.");

try
{
    var settingsService = new JsonSettingsService();
    var settings = await settingsService.GetSettingsAsync();
    using var slaveService = new ModbusSlaveService(settings);

    using var shutdownCts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        shutdownCts.Cancel();
    };

    await slaveService.StartAsync(shutdownCts.Token);

    Logger.Log("[APP] Press Ctrl+C to stop.");
    try
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, shutdownCts.Token);
    }
    catch (OperationCanceledException)
    {
    }

    await slaveService.StopAsync();
}
catch (Exception exception)
{
    Logger.LogException(exception, "Program.Main");
    Environment.ExitCode = 1;
}
finally
{
    Logger.Log("[APP] Hitachi Modbus Slave simulator stopped.");
}
