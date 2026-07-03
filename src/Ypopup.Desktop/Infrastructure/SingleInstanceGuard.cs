namespace Ypopup.Desktop.Infrastructure;

/// <summary>단일 인스턴스 실행을 Mutex로 보장합니다.</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex? _mutex;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(true, "Global\\Ypopup-SingleInstance-Mutex", out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public bool IsPrimaryInstance => _mutex is not null;

    public void Dispose()
    {
        _mutex?.Dispose();
    }
}
