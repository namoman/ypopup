using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Ypopup.Desktop.Application;

namespace Ypopup.Desktop;

public partial class App : Avalonia.Application
{
    private ApplicationHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host = new ApplicationHost();
        if (!await _host.TryStartAsync())
        {
            desktop.Shutdown();
            return;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static ApplicationHost? CurrentHost => (Current as App)?._host;

    public static async Task ShutdownAppAsync()
    {
        if (Current is App app && app._host is not null)
        {
            await app._host.DisposeAsync();
            app._host = null;
        }

        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
