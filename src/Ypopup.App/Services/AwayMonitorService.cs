using System.Runtime.InteropServices;
using System.Windows.Threading;
using Ypopup.Network;

namespace Ypopup.App.Services;

public sealed class AwayMonitorService : IDisposable
{
    private readonly YpopupCoordinator _coordinator;
    private readonly DispatcherTimer _timer;

    public AwayMonitorService(YpopupCoordinator coordinator)
    {
        _coordinator = coordinator;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
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
        _coordinator.IsAway = settings.AwayEnabledByIdle && IsIdle(settings.AwayIdleMinutes);
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    private static bool IsIdle(int idleMinutes)
    {
        if (idleMinutes <= 0)
        {
            return false;
        }

        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return false;
        }

        var idleMs = unchecked((uint)Environment.TickCount - info.DwTime);
        return idleMs >= (uint)idleMinutes * 60_000;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
