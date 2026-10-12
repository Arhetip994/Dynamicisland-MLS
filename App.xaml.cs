using System.IO;
using System.Windows;

namespace DynamicIsland;

public partial class App : Application
{
    const string InstanceName = "DynamicIsland.SingleInstance";
    const string LogFileName = "DynamicIsland.log";

    Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Log(ex);
        };

        try
        {
            _mutex = new Mutex(true, InstanceName, out bool isOnlyInstance);
            if (!isOnlyInstance)
            {
                Log("Shutdown: Mutex not acquired (another instance running)");
                Shutdown();
                return;
            }

            base.OnStartup(e);
            DispatcherUnhandledException += (_, args) =>
            {
                Log(args.Exception);
                args.Handled = true;
            };
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            Log(ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch { }
    }

    public static void Log(string msg)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), LogFileName),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    public static void Log(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), LogFileName),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }
}
