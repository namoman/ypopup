namespace Ypopup.Core.Startup;

public static class StartupLaunchOptions
{
    public const string TrayOnlyFlag = "--tray";

    public static bool IsTrayOnlyLaunch(IEnumerable<string>? args)
    {
        return args?.Any(arg => string.Equals(arg, TrayOnlyFlag, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
