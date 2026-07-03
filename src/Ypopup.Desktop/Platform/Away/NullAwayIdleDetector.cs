namespace Ypopup.Desktop.Platform.Away;

public sealed class NullAwayIdleDetector : IAwayIdleDetector
{
    public bool IsIdle(int idleMinutes) => false;
}
