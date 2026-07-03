using Ypopup.Core.Models;

namespace Ypopup.Desktop.Platform.Firewall;

public sealed class StubFirewallService : IFirewallService
{
    public FirewallStatus GetStatus(AppSettings settings)
    {
        return new FirewallStatus(
            FirewallRuleStatus.Unknown,
            false,
            false,
            Environment.ProcessPath ?? string.Empty);
    }

    public string GetStatusSummary(FirewallStatus status, AppSettings settings)
    {
        return "방화벽: OS 설정에서 UDP/TCP 포트를 허용하세요.\n" +
               $"UDP {settings.DiscoveryPort}, TCP {settings.TcpPort}, 공유폴더 TCP {settings.ShareFolderPort}";
    }

    public bool TryAddFirewallRules(AppSettings settings, out string message)
    {
        message = "자동 방화벽 규칙 추가는 Windows에서만 지원됩니다. 시스템 방화벽에서 포트를 허용하세요.";
        return false;
    }

    public void OpenFirewallSettings()
    {
        throw new PlatformNotSupportedException("이 OS에서는 Windows 방화벽 설정을 열 수 없습니다.");
    }
}
