using System;
using System.IO;
using System.Text.Json;
using ModbusBridge.Models;

namespace ModbusBridge.Utilities;

public static class ConfigBootstrapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static void EnsureConfigFile()
    {
        Directory.CreateDirectory(AppStoragePaths.RootDirectory);

        if (File.Exists(AppStoragePaths.SettingsPath))
        {
            Logger.Log($"[CONFIG] Loaded config path: {AppStoragePaths.SettingsPath}");
            return;
        }

        var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(defaultConfigPath))
        {
            File.Copy(defaultConfigPath, AppStoragePaths.SettingsPath);
            Logger.Log($"[CONFIG] Created default config from appsettings.json: {AppStoragePaths.SettingsPath}");
            return;
        }

        var json = JsonSerializer.Serialize(CreateDefaultSettings(), SerializerOptions);
        File.WriteAllText(AppStoragePaths.SettingsPath, json);
        Logger.Log($"[CONFIG] Created default config: {AppStoragePaths.SettingsPath}");
    }

    private static ApplicationSettings CreateDefaultSettings()
    {
        return new ApplicationSettings
        {
            Siemens = new SiemensSettings
            {
                IpAddress = "192.168.1.10",
                CpuType = "S71200",
                Port = 102,
                Rack = 0,
                Slot = 1,
                WriteIntervalMs = 100,
                AutoReconnect = true
            },
            Modbus = new ModbusSettings
            {
                BaudRate = 38400,
                Parity = "Even",
                DataBits = 8,
                StopBits = "One",
                RequestIntervalMs = 10,
                TimeoutMs = 100,
                RetryCount = 1,
                RegisterAddressMode = "Vendor4x",
                Registers = [408502, 408503, 405202, 405213, 403202],
                Channels =
                [
                    new ModbusChannelSettings
                    {
                        Name = "COM1",
                        PortName = "COM1",
                        FirstSlaveId = 1,
                        LastSlaveId = 34,
                        PlcMemoryStart = 3000
                    },
                    new ModbusChannelSettings
                    {
                        Name = "COM2",
                        PortName = "COM2",
                        FirstSlaveId = 35,
                        LastSlaveId = 58,
                        PlcMemoryStart = 4000
                    },
                    new ModbusChannelSettings
                    {
                        Name = "COM3",
                        PortName = "COM3",
                        FirstSlaveId = 59,
                        LastSlaveId = 74,
                        PlcMemoryStart = 5000
                    }
                ]
            },
            Ui = new UiSettings
            {
                RefreshIntervalMs = 500
            }
        };
    }
}
