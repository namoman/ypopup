using Xunit;
using Ypopup.Core.Network;

namespace Ypopup.Core.Tests.Network;

public class ConnectionLimiterTests
{
    [Fact]
    public async Task WaitAsync_UpToMax_AcquiredImmediately()
    {
        using var limiter = new ConnectionLimiter(maxConcurrent: 3);
        var handles = new List<IDisposable>();

        for (var i = 0; i < 3; i++)
        {
            var handle = await limiter.WaitAsync(CancellationToken.None);
            handles.Add(handle);
        }

        Assert.Equal(3, handles.Count);

        foreach (var handle in handles)
        {
            handle.Dispose();
        }
    }

    [Fact]
    public async Task WaitAsync_ExceedingMax_BlocksUntilReleased()
    {
        using var limiter = new ConnectionLimiter(maxConcurrent: 1);
        var firstHandle = await limiter.WaitAsync(CancellationToken.None);

        var acquired = false;
        var task = Task.Run(async () =>
        {
            using var secondHandle = await limiter.WaitAsync(CancellationToken.None);
            acquired = true;
        });

        await Task.Delay(50);
        Assert.False(acquired);

        firstHandle.Dispose();
        await task;

        Assert.True(acquired);
    }

    [Fact]
    public async Task WaitAsync_Cancelled_ThrowsOperationCanceled()
    {
        using var limiter = new ConnectionLimiter(maxConcurrent: 1);
        using var firstHandle = await limiter.WaitAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => limiter.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task Release_DoubleRelease_DoesNotThrow()
    {
        using var limiter = new ConnectionLimiter(maxConcurrent: 1);
        var handle = await limiter.WaitAsync(CancellationToken.None);

        handle.Dispose();

        var secondHandle = await limiter.WaitAsync(CancellationToken.None);
        secondHandle.Dispose();
    }

    [Fact]
    public void Constructor_ZeroOrNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionLimiter(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionLimiter(-1));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var limiter = new ConnectionLimiter(maxConcurrent: 2);
        await limiter.DisposeAsync();
        await limiter.DisposeAsync();
    }
}