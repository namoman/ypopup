namespace Ypopup.Desktop.Platform.Startup;

public static class StartupServiceFactory
{
    public static IStartupService Create() =>
        OperatingSystem.IsWindows()
            ? new WindowsStartupService()
            : new NullStartupService();
}
