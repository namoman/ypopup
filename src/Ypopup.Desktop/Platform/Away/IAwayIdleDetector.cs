namespace Ypopup.Desktop.Platform.Away;

public interface IAwayIdleDetector
{
    bool IsIdle(int idleMinutes);
}
