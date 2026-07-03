using Ypopup.Core.Models;

namespace Ypopup.Desktop.Platform.Firewall;

public interface IFirewallService
{
    FirewallStatus GetStatus(AppSettings settings);

    string GetStatusSummary(FirewallStatus status, AppSettings settings);

    bool TryAddFirewallRules(AppSettings settings, out string message);

    void OpenFirewallSettings();
}
