using LaserCollisionIn3DObjects.Wpf.Services;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class ApplicationLifetimeTests
{
    [Fact]
    public void RequestShutdown_IsSafeAndIdempotent()
    {
        using var lifetime = new ApplicationLifetime();
        Assert.False(lifetime.Token.IsCancellationRequested);

        lifetime.RequestShutdown();
        lifetime.RequestShutdown();

        Assert.True(lifetime.Token.IsCancellationRequested);
    }
}
