using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModbusBridge.Models;
using ModbusBridge.Services;

namespace ModbusBridge.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService? _settingsService;
    private ApplicationSettings _settings = new();

    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private int _port;
    [ObservableProperty] private int _rack;
    [ObservableProperty] private int _slot;
    [ObservableProperty] private int _baudRate;
    [ObservableProperty] private string _parity = "Even";
    [ObservableProperty] private int _dataBits;
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public IReadOnlyList<string> ParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public IReadOnlyList<string> StopBitsOptions { get; } = ["One", "Two", "OnePointFive"];
    public ObservableCollection<SettingsChannelViewModel> Channels { get; } = [];

    public event EventHandler<bool>? RequestClose;

    public SettingsViewModel()
    {
    }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task LoadAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        _settings = await _settingsService.GetSettingsAsync();

        IpAddress = _settings.Siemens.IpAddress;
        Port = _settings.Siemens.Port;
        Rack = _settings.Siemens.Rack;
        Slot = _settings.Siemens.Slot;
        BaudRate = _settings.Modbus.BaudRate;
        Parity = _settings.Modbus.Parity;
        DataBits = _settings.Modbus.DataBits;
        StopBits = _settings.Modbus.StopBits;
        StatusMessage = string.Empty;

        Channels.Clear();
        foreach (var channel in _settings.Modbus.Channels)
        {
            Channels.Add(new SettingsChannelViewModel
            {
                Enable = channel.Enable,
                Name = channel.Name,
                PortName = channel.PortName,
                FirstSlaveId = channel.FirstSlaveId,
                LastSlaveId = channel.LastSlaveId,
                PlcMemoryStart = channel.PlcMemoryStart
            });
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(IpAddress))
        {
            StatusMessage = "PLC IP is required.";
            return;
        }

        _settings.Siemens.IpAddress = IpAddress.Trim();
        _settings.Siemens.Port = Math.Max(1, Port);
        _settings.Siemens.Rack = Math.Max(0, Rack);
        _settings.Siemens.Slot = Math.Max(0, Slot);
        _settings.Modbus.BaudRate = Math.Max(1, BaudRate);
        _settings.Modbus.Parity = string.IsNullOrWhiteSpace(Parity) ? "Even" : Parity;
        _settings.Modbus.DataBits = Math.Max(5, DataBits);
        _settings.Modbus.StopBits = string.IsNullOrWhiteSpace(StopBits) ? "One" : StopBits;

        for (var index = 0; index < Channels.Count && index < _settings.Modbus.Channels.Count; index++)
        {
            var source = Channels[index];
            var target = _settings.Modbus.Channels[index];
            target.Enable = source.Enable;
            target.Name = source.Name.Trim();
            target.PortName = source.PortName.Trim();
            target.FirstSlaveId = source.FirstSlaveId;
            target.LastSlaveId = source.LastSlaveId;
            target.PlcMemoryStart = Math.Max(0, source.PlcMemoryStart);
        }

        await _settingsService.SetSettingsAsync(_settings);
        StatusMessage = "Settings saved.";
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
