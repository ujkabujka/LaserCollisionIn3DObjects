using LaserCollisionIn3DObjects.Wpf.Commands;

namespace LaserCollisionIn3DObjects.Tests.Commands;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_DisablesCommandAndRejectsReentrancyUntilCompletion()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var command = new AsyncRelayCommand(async () =>
        {
            executions++;
            started.SetResult();
            await release.Task;
        });

        var running = command.ExecuteAsync();
        await started.Task;
        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.Equal(1, executions);

        release.SetResult();
        await running;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ExecuteAsync_RestoresAvailabilityAndPropagatesException()
    {
        var command = new AsyncRelayCommand(() => throw new InvalidOperationException("failure"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(command.ExecuteAsync);

        Assert.Equal("failure", exception.Message);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ExecuteAsync_RaisesCanExecuteChangedAtStartAndFinish()
    {
        var notifications = 0;
        var command = new AsyncRelayCommand(() => Task.CompletedTask);
        command.CanExecuteChanged += (_, _) => notifications++;

        await command.ExecuteAsync();

        Assert.Equal(2, notifications);
    }
}
