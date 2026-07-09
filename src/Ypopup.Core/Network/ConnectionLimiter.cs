namespace Ypopup.Core.Network;

public sealed class ConnectionLimiter : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private int _disposed;

    public int MaxConcurrent { get; }

    public ConnectionLimiter(int maxConcurrent)
    {
        if (maxConcurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "최대 동시 처리 수는 1 이상이어야 합니다.");
        }

        MaxConcurrent = maxConcurrent;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(this);
    }

    public void Release()
    {
        if (_disposed != 0)
        {
            return;
        }

        _semaphore.Release();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _semaphore.Dispose();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly ConnectionLimiter _owner;
        private int _released;

        public Releaser(ConnectionLimiter owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            _owner.Release();
        }
    }
}