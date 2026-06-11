using System.Text;

namespace ModbusSlave.Utilities;

public static class Logger
{
    private static readonly object SyncRoot = new();

    public static string LogDirectory
    {
        get
        {
            var directory = Path.Combine(AppStoragePaths.RootDirectory, "Logs");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string LogPath => Path.Combine(LogDirectory, "modbus-slave.log");

    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] {message}";
        Console.WriteLine(line);

        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break simulator flow.
        }
    }

    public static void LogException(Exception exception, string source)
    {
        Log($"{source} exception: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception}");
    }
}
