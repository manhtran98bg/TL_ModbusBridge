using System;
using System.IO;
using System.Text.Json;
using ModbusBridge.Models;

namespace ModbusBridge.Utilities;

public static class AppStoragePaths
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public const string AppDirectoryName = ".modbus_bridge";
    public const string ConfigFileName = "config.json";

    public static string RootDirectory
    {
        get
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var directory = Path.Combine(userProfile, AppDirectoryName);
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string DataDirectory
    {
        get
        {
            var directory = Path.Combine(RootDirectory, "Data");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string LogDirectory
    {
        get
        {
            var directory = Path.Combine(RootDirectory, "Logs");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string SettingsPath => Path.Combine(RootDirectory, ConfigFileName);

    public static void EnsureConfigFile()
    {
        Directory.CreateDirectory(RootDirectory);

        if (File.Exists(SettingsPath))
        {
            Logger.Log($"[CONFIG] Loaded config path: {SettingsPath}");
            return;
        }

        var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(defaultConfigPath))
        {
            File.Copy(defaultConfigPath, SettingsPath);
            Logger.Log($"[CONFIG] Created default config from appsettings.json: {SettingsPath}");
            return;
        }

        var json = JsonSerializer.Serialize(CreateDefaultSettings(), SerializerOptions);
        File.WriteAllText(SettingsPath, json);
        Logger.Log($"[CONFIG] Created default config: {SettingsPath}");
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
                WriteIntervalMs = 1000,
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
                MaxConsecutiveErrors = 20,
                ReconnectDelayMs = 1000,
                MaxReconnectDelayMs = 10000,
                LogReadErrors = false,
                RegisterAddressMode = "Vendor4x",
                Registers = [408502, 408503, 405202, 405213, 403202],
                Channels =
                [
                    new ModbusChannelSettings
                    {
                        Enable = true,
                        Name = "COM1",
                        PortName = "COM1",
                        FirstSlaveId = 1,
                        LastSlaveId = 34,
                        PlcMemoryStart = 3000
                    },
                    new ModbusChannelSettings
                    {
                        Enable = true,
                        Name = "COM2",
                        PortName = "COM2",
                        FirstSlaveId = 35,
                        LastSlaveId = 58,
                        PlcMemoryStart = 4000
                    },
                    new ModbusChannelSettings
                    {
                        Enable = true,
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
