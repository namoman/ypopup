namespace Ypopup.Desktop.Platform.Firewall;

public enum FirewallRuleStatus
{
    Unknown,
    NotConfigured,
    Configured
}

public sealed record FirewallStatus(
    FirewallRuleStatus RuleStatus,
    bool UdpPortOpen,
    bool TcpPortOpen,
    string ExecutablePath);
