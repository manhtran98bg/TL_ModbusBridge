using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using ModbusBridge.Utilities;

namespace ModbusBridge;

sealed class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    public static void Main(string[] args)
    {
        AllocConsole();
        RegisterCrashLogging();
        Logger.Log("[APP] Hitachi Modbus Bridge starting.");

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            CrashLogService.LogException(exception, "Program.Main");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void RegisterCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            CrashLogService.LogException(e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogService.LogException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }
}
