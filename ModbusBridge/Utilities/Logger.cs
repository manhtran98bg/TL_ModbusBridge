using System;
using System.IO;
using System.Text;

namespace ModbusBridge.Utilities;

public static class Logger
{
    private static readonly object SyncRoot = new();

    public static string LogPath => Path.Combine(AppStoragePaths.LogDirectory, "logger.log");
    public static string CrashLogPath => Path.Combine(AppStoragePaths.LogDirectory, "crash.log");

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

    public static void LogCrashException(object? exceptionObject, string source)
    {
        if (exceptionObject is Exception exception)
        {
            LogCrashException(exception, source);
            return;
        }

        WriteCrashLine($"{source} non-exception crash object: {exceptionObject}");
    }

    public static void LogCrashException(Exception exception, string source)
    {
        WriteCrashLine($"{source} exception: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception}");
    }

    private static void WriteCrashLine(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] {message}{Environment.NewLine}";

        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(CrashLogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Crash logging must never throw while handling a crash.
        }
    }
}
