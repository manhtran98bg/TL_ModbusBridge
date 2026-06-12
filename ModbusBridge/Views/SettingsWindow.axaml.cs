using System;
using Avalonia.Controls;
using ModbusBridge.ViewModels;

namespace ModbusBridge.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.RequestClose -= OnRequestClose;
        }

        _viewModel = DataContext as SettingsViewModel;

        if (_viewModel is not null)
        {
            _viewModel.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, bool result)
    {
        Close(result);
    }
}
