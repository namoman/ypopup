namespace Ypopup.Desktop.Platform.Away;

public static class AwayIdleDetectorFactory
{
    public static IAwayIdleDetector Create() =>
        OperatingSystem.IsWindows()
            ? new WindowsAwayIdleDetector()
            : new NullAwayIdleDetector();
}
