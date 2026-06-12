using CommunityToolkit.Mvvm.ComponentModel;

namespace ModbusBridge.ViewModels;

public partial class SettingsChannelViewModel : ViewModelBase
{
    [ObservableProperty] private bool _enable;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _portName = string.Empty;
    [ObservableProperty] private byte _firstSlaveId;
    [ObservableProperty] private byte _lastSlaveId;
    [ObservableProperty] private int _plcMemoryStart;
}
