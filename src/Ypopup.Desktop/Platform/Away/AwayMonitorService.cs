using Avalonia.Threading;
using Ypopup.Network;

namespace Ypopup.Desktop.Platform.Away;

public sealed class AwayMonitorService : IDisposable
{
    private readonly YpopupCoordinator _coordinator;
    private readonly IAwayIdleDetector _idleDetector;
    private readonly DispatcherTimer _timer;

    public AwayMonitorService(YpopupCoordinator coordinator, IAwayIdleDetector idleDetector)
    {
        _coordinator = coordinator;
        _idleDetector = idleDetector;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => RefreshAwayStatus();
    }

    public void Start()
    {
        _timer.Start();
        RefreshAwayStatus();
    }

    public void RefreshAwayStatus()
    {
        var settings = _coordinator.Settings;
        _coordinator.IsAway = settings.AwayEnabledByIdle
                              && _idleDetector.IsIdle(settings.AwayIdleMinutes);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
