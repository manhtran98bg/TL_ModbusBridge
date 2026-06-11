using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModbusBridge.Models;
using ModbusBridge.Services;
using ModbusBridge.Utilities;
using ModbusBridge.ViewModels;
using ModbusBridge.Views;

namespace ModbusBridge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            Services = ConfigureServices();

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };

            desktop.ShutdownRequested += async (_, _) =>
            {
                await Services.GetRequiredService<IBridgeService>().StopAsync();
                _serviceProvider?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ServiceProvider ConfigureServices()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<ApplicationSettings>(configuration);

        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<RegisterCache>();
        services.AddSingleton<IModbusService, ModbusService>();
        services.AddSingleton<IPlcService, PlcService>();
        services.AddSingleton<IBridgeService, BridgeService>();
        services.AddTransient<MainWindowViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider;
    }

    private static IConfiguration BuildConfiguration()
    {
        AppStoragePaths.EnsureConfigFile();

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(AppStoragePaths.SettingsPath, optional: false, reloadOnChange: true)
            .Build();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
