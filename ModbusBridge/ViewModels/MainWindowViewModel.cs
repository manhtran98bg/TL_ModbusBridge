using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModbusBridge.Models;
using ModbusBridge.Services;

namespace ModbusBridge.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IBridgeService? _bridgeService;
    private readonly IStatisticsService? _statisticsService;

    [ObservableProperty] private string _title = "Hitachi Modbus Bridge";
    [ObservableProperty] private string _statusMessage = "Stopped";
    [ObservableProperty] private long _modbusRequestCount;
    [ObservableProperty] private long _modbusErrorCount;
    [ObservableProperty] private long _plcWriteCount;
    [ObservableProperty] private long _plcErrorCount;

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(IBridgeService bridgeService, IStatisticsService statisticsService)
    {
        _bridgeService = bridgeService;
        _statisticsService = statisticsService;
        _bridgeService.StatusChanged += OnBridgeStatusChanged;
        RefreshSnapshot();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (_bridgeService is null)
        {
            return;
        }

        await _bridgeService.StartAsync();
        RefreshSnapshot();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (_bridgeService is null)
        {
            return;
        }

        await _bridgeService.StopAsync();
        RefreshSnapshot();
    }

    [RelayCommand]
    private void RefreshSnapshot()
    {
        if (_statisticsService is null)
        {
            return;
        }

        BridgeStatistics statistics = _statisticsService.GetSnapshot();
        ModbusRequestCount = statistics.ModbusRequestCount;
        ModbusErrorCount = statistics.ModbusErrorCount;
        PlcWriteCount = statistics.PlcWriteCount;
        PlcErrorCount = statistics.PlcErrorCount;
    }

    private void OnBridgeStatusChanged(object? sender, BridgeStatusChangedEventArgs e)
    {
        StatusMessage = e.Message;
        RefreshSnapshot();
    }
}
