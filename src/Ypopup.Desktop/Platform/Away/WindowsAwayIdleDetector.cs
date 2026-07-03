using System.Runtime.InteropServices;

namespace Ypopup.Desktop.Platform.Away;

public sealed class WindowsAwayIdleDetector : IAwayIdleDetector
{
    public bool IsIdle(int idleMinutes)
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
