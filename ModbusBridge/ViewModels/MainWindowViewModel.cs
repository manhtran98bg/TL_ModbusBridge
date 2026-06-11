using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using ModbusBridge.Models;
using ModbusBridge.Services;

namespace ModbusBridge.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IBridgeService? _bridgeService;
    private readonly IStatisticsService? _statisticsService;
    private readonly DispatcherTimer? _refreshTimer;

    [ObservableProperty] private string _title = "Hitachi Modbus Bridge";
    [ObservableProperty] private string _statusMessage = "Stopped";
    [ObservableProperty] private long _modbusRequestCount;
    [ObservableProperty] private long _modbusErrorCount;
    [ObservableProperty] private long _plcWriteCount;
    [ObservableProperty] private long _plcErrorCount;

    public ObservableCollection<ChannelStatusViewModel> ChannelStatuses { get; } = [];

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(
        IBridgeService bridgeService,
        IStatisticsService statisticsService,
        IOptionsMonitor<ApplicationSettings> options)
    {
        _bridgeService = bridgeService;
        _statisticsService = statisticsService;
        _bridgeService.StatusChanged += OnBridgeStatusChanged;
        RefreshSnapshot();

        var refreshIntervalMs = Math.Max(100, options.CurrentValue.Ui.RefreshIntervalMs);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(refreshIntervalMs)
        };
        _refreshTimer.Tick += (_, _) => RefreshSnapshot();
        _refreshTimer.Start();
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

        BridgeSnapshot snapshot = _bridgeService?.GetSnapshot()
            ?? new BridgeSnapshot { Statistics = _statisticsService.GetSnapshot() };
        BridgeStatistics statistics = snapshot.Statistics;
        ModbusRequestCount = statistics.ModbusRequestCount;
        ModbusErrorCount = statistics.ModbusErrorCount;
        PlcWriteCount = statistics.PlcWriteCount;
        PlcErrorCount = statistics.PlcErrorCount;
        UpdateChannelStatuses(snapshot);
    }

    private void OnBridgeStatusChanged(object? sender, BridgeStatusChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = e.Message;
            RefreshSnapshot();
        });
    }

    private void UpdateChannelStatuses(BridgeSnapshot snapshot)
    {
        var modbusWorkers = snapshot.Workers
            .Where(worker => worker.Kind == WorkerKind.Modbus)
            .OrderBy(worker => worker.Name)
            .ToArray();

        foreach (var removed in ChannelStatuses
                     .Where(item => modbusWorkers.All(worker => worker.Name != item.Name))
                     .ToArray())
        {
            ChannelStatuses.Remove(removed);
        }

        foreach (var worker in modbusWorkers)
        {
            var item = ChannelStatuses.FirstOrDefault(channel => channel.Name == worker.Name);
            if (item is null)
            {
                item = new ChannelStatusViewModel();
                ChannelStatuses.Add(item);
            }

            item.Update(worker);
        }
    }
}
