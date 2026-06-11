using System;
using System.IO;
using System.Text;

namespace ModbusBridge.Utilities;

public static class Logger
{
    private static readonly object SyncRoot = new();

    public static string LogPath => Path.Combine(AppStoragePaths.LogDirectory, "logger.log");

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
            // Logging must never break app flow.
        }
    }

    public static void LogException(Exception exception, string source)
    {
        Log($"{source} exception: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception}");
    }
}
