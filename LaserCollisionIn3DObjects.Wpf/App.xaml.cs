using System.Windows.Threading;
using LaserCollisionIn3DObjects.Wpf.Services;
using System.Diagnostics;

namespace LaserCollisionIn3DObjects.Wpf;

public partial class App : System.Windows.Application
{
    public ApplicationLogService AppLog { get; } = new();
    public ApplicationLifetime Lifetime { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        Trace.WriteLine("[Startup] App.OnStartup entered.");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Trace.WriteLine("[Shutdown] App.OnExit entered.");
        Lifetime.RequestShutdown();
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        base.OnExit(e);
        Lifetime.Dispose();
        Trace.WriteLine("[Shutdown] App.OnExit completed.");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Trace.WriteLine($"[Startup] Unhandled UI exception: {e.Exception}");
        AppLog.LogError("Unhandled UI exception.", e.Exception, nameof(App));
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Trace.WriteLine($"[Startup] Unhandled AppDomain exception: {exception}");
        AppLog.LogError("Unhandled AppDomain exception.", exception, nameof(App));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Trace.WriteLine($"[Startup] Unobserved task exception: {e.Exception}");
        AppLog.LogError("Unobserved task exception.", e.Exception, nameof(App));
        e.SetObserved();
    }
}
