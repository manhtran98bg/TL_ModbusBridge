using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ModbusBridge.Models;

namespace ModbusBridge.ViewModels;

public partial class ChannelStatusViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _portName = string.Empty;
    [ObservableProperty] private string _state = WorkerState.Stopped.ToString();
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private long _requestCount;
    [ObservableProperty] private long _errorCount;
    [ObservableProperty] private string _lastReadElapsed = "-";
    [ObservableProperty] private string _lastSuccessTime = "-";
    [ObservableProperty] private string _lastErrorTime = "-";
    [ObservableProperty] private string _lastTarget = "-";

    public void Update(WorkerStatus status)
    {
        Name = status.Name;
        PortName = status.PortName;
        State = status.State.ToString();
        Message = status.Message;
        RequestCount = status.RequestCount;
        ErrorCount = status.ErrorCount;
        LastReadElapsed = status.LastReadElapsed == TimeSpan.Zero
            ? "-"
            : $"{status.LastReadElapsed.TotalMilliseconds:0.0} ms";
        LastSuccessTime = FormatTime(status.LastSuccessTimestamp);
        LastErrorTime = FormatTime(status.LastErrorTimestamp);
        LastTarget = status.LastSlaveId == 0
            ? "-"
            : $"ID {status.LastSlaveId}, {status.LastStartAddress} x{status.LastQuantity}";
    }

    private static string FormatTime(DateTime value)
    {
        return value == DateTime.MinValue ? "-" : value.ToString("HH:mm:ss");
    }
}
