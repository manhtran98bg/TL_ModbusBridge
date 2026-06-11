using System;
using System.IO;
using System.Text;

namespace ModbusBridge.Utilities;

public static class CrashLogService
{
    public static string CrashLogPath => Path.Combine(AppStoragePaths.LogDirectory, "crash.log");

    public static void LogException(object? exceptionObject, string source)
    {
        if (exceptionObject is Exception exception)
        {
            LogException(exception, source);
            return;
        }

        WriteCrashLine($"{source} non-exception crash object: {exceptionObject}");
    }

    public static void LogException(Exception exception, string source)
    {
        WriteCrashLine($"{source} exception: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception}");
    }

    private static void WriteCrashLine(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] {message}{Environment.NewLine}";

        try
        {
            File.AppendAllText(CrashLogPath, line, Encoding.UTF8);
        }
        catch
        {
            // Crash logging must never throw while handling a crash.
        }
    }
}
