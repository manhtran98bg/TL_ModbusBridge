using System;
using System.IO;

namespace ModbusBridge.Utilities;

public static class AppStoragePaths
{
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
}
