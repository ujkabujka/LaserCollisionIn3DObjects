using System.Diagnostics;

namespace LaserCollisionIn3DObjects.Wpf.Services;

/// <summary>Owns the cancellation signal shared by application background work.</summary>
public sealed class ApplicationLifetime : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private int _shutdownRequested;

    public CancellationToken Token => _shutdown.Token;
    public bool IsShutdownRequested => Token.IsCancellationRequested;

    public void RequestShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0) return;
        Trace.WriteLine("[Shutdown] Application cancellation requested.");
        _shutdown.Cancel();
    }

    public void Dispose() => _shutdown.Dispose();
}
