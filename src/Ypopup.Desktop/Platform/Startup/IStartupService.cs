namespace Ypopup.Desktop.Platform.Startup;

public interface IStartupService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);

    void EnsureTrayLaunchRegistered();
}
