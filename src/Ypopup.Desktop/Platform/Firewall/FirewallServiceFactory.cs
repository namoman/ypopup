using Ypopup.Core.Models;

namespace Ypopup.Desktop.Platform.Firewall;

public static class FirewallServiceFactory
{
    public static IFirewallService Create() =>
        OperatingSystem.IsWindows()
            ? new WindowsFirewallService()
            : new StubFirewallService();
}
