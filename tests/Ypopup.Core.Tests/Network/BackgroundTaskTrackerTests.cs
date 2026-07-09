using Xunit;
using Ypopup.Core.Helpers;

namespace Ypopup.Core.Tests.Network;

public class BackgroundTaskTrackerTests
{
    [Fact]
    public async Task RunAsync_SuccessfulTask_CompletesWithoutError()
    {
        var completed = false;

        await BackgroundTaskTracker.RunAsync("test-ok", async () =>
        {
            await Task.Delay(10);
            completed = true;
        });

        Assert.True(completed);
    }

    [Fact]
    public async Task RunAsync_ThrowingTask_InvokesOnErrorAndDoesNotPropagate()
    {
        var errorReported = false;
        Exception? captured = null;

        await BackgroundTaskTracker.RunAsync(
            "test-fail",
            () => throw new InvalidOperationException("boom"),
            (_, ex) =>
            {
                errorReported = true;
                captured = ex;
            });

        Assert.True(errorReported);
        Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("boom", captured!.Message);
    }

    [Fact]
    public async Task RunAsync_OperationCanceled_DoesNotInvokeOnError()
    {
        var onErrorCalled = false;

        await BackgroundTaskTracker.RunAsync(
            "test-cancel",
            () => throw new OperationCanceledException(),
            (_, _) => onErrorCalled = true);

        Assert.False(onErrorCalled);
    }

    [Fact]
    public async Task RunAsync_NonAsyncFactory_WorksGracefully()
    {
        var ran = false;
        await BackgroundTaskTracker.RunAsync("sync", () =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.True(ran);
    }
}