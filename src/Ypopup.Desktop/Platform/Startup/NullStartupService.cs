namespace Ypopup.Desktop.Platform.Startup;

public sealed class NullStartupService : IStartupService
{
    public bool IsEnabled() => false;

    public void SetEnabled(bool enabled)
    {
        // macOS/Linux: 로그인 항목은 추후 플랫폼별 구현
    }

    public void EnsureTrayLaunchRegistered()
    {
    }
}
