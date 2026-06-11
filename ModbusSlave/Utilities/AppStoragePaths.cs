using System.Text.Json;
using ModbusSlave.Models;

namespace ModbusSlave.Utilities;

public static class AppStoragePaths
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public const string AppDirectoryName = ".modbus_bridge";
    public const string BridgeConfigFileName = "config.json";
    public const string ConfigFileName = "slave_config.json";

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

    public static string BridgeSettingsPath => Path.Combine(RootDirectory, BridgeConfigFileName);
    public static string SettingsPath => Path.Combine(RootDirectory, ConfigFileName);

    public static void EnsureConfigFile()
    {
        if (File.Exists(SettingsPath))
        {
            Logger.Log($"[CONFIG] Slave config: {SettingsPath}");
            return;
        }

        var settings = File.Exists(BridgeSettingsPath)
            ? LoadSettings(BridgeSettingsPath)
            : CreateDefaultSettings();

        ApplyDefaultSlavePorts(settings);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
        Logger.Log($"[CONFIG] Created slave config: {SettingsPath}");
    }

    private static ApplicationSettings LoadSettings(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ApplicationSettings>(json, SerializerOptions)
            ?? CreateDefaultSettings();
    }

    private static void ApplyDefaultSlavePorts(ApplicationSettings settings)
    {
        foreach (var channel in settings.Modbus.Channels)
        {
            channel.PortName = CreateDefaultSlavePort(channel.PortName);
        }
    }

    private static string CreateDefaultSlavePort(string sourcePortName)
    {
        const string comPrefix = "COM";

        if (sourcePortName.StartsWith(comPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(sourcePortName[comPrefix.Length..], out var portNumber))
        {
            return $"{comPrefix}{portNumber + 100}";
        }

        return sourcePortName;
    }

    private static ApplicationSettings CreateDefaultSettings()
    {
        return new ApplicationSettings
        {
            Modbus =
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
                        PortName = "COM101",
                        FirstSlaveId = 1,
                        LastSlaveId = 34,
                        PlcMemoryStart = 3000
                    },
                    new ModbusChannelSettings
                    {
                        Enable = true,
                        Name = "COM2",
                        PortName = "COM102",
                        FirstSlaveId = 35,
                        LastSlaveId = 58,
                        PlcMemoryStart = 4000
                    },
                    new ModbusChannelSettings
                    {
                        Enable = true,
                        Name = "COM3",
                        PortName = "COM103",
                        FirstSlaveId = 59,
                        LastSlaveId = 74,
                        PlcMemoryStart = 5000
                    }
                ]
            }
        };
    }
}
