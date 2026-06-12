using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ModbusBridge.Models;

namespace ModbusBridge.ViewModels;

public partial class ChannelStatusViewModel : ViewModelBase
{
    private static readonly IBrush NormalBackground = Brush.Parse("#FFFFFF");
    private static readonly IBrush FlashBackground = Brush.Parse("#EAF3FF");
    private int _flashVersion;

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
    [ObservableProperty] private IBrush _cardBackground = NormalBackground;

    public void Update(WorkerStatus status)
    {
        var nextState = status.State.ToString();
        var shouldFlash = RequestCount != status.RequestCount
            || ErrorCount != status.ErrorCount
            || State != nextState;

        Name = status.Name;
        PortName = status.PortName;
        State = nextState;
        Message = status.Message;
        RequestCount = status.RequestCount;
        ErrorCount = status.ErrorCount;
        LastReadElapsed = status.LastReadElapsed == TimeSpan.Zero
            ? "-"
            : $"{status.LastReadElapsed.TotalMilliseconds:0.0} ms";
        LastSuccessTime = FormatTime(status.LastSuccessTimestamp);
        LastErrorTime = FormatTime(status.LastErrorTimestamp);
        LastTarget = FormatTarget(status);

        if (shouldFlash)
        {
            FlashCard();
        }
    }

    private static string FormatTime(DateTime value)
    {
        return value == DateTime.MinValue ? "-" : value.ToString("HH:mm:ss");
    }

    private static string FormatTarget(WorkerStatus status)
    {
        if (status.Kind == WorkerKind.Plc)
        {
            return status.LastQuantity == 0
                ? "-"
                : $"M{status.LastStartAddress}, {status.LastQuantity} bytes";
        }

        return status.LastSlaveId == 0
            ? "-"
            : $"ID {status.LastSlaveId}, {status.LastStartAddress} x{status.LastQuantity}";
    }

    private async void FlashCard()
    {
        var version = ++_flashVersion;
        CardBackground = FlashBackground;
        await Task.Delay(180);

        if (version == _flashVersion)
        {
            CardBackground = NormalBackground;
        }
    }
}
