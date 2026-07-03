using Avalonia.Threading;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Infrastructure;
using Ypopup.Desktop.Platform.Away;
using Ypopup.Desktop.Platform.Notifications;
using Ypopup.Desktop.Tray;
using Ypopup.Desktop.Windows;
using Ypopup.Network;

namespace Ypopup.Desktop.Application;

/// <summary>앱 시작·종료와 하위 모듈 조율만 담당합니다.</summary>
public sealed class ApplicationHost : IAsyncDisposable
{
    private readonly SingleInstanceGuard _singleInstance = new();
    private readonly TrayIconManager _trayIconManager = new();
    private readonly INotificationSoundService _notificationSound = new NotificationSoundService();

    private YpopupCoordinator? _coordinator;
    private AwayMonitorService? _awayMonitor;
    private WindowNavigator? _windowNavigator;
    private IncomingMessagePresenter? _messagePresenter;
    private int _disposed;

    public YpopupCoordinator? Coordinator => _coordinator;

    public async Task<bool> TryStartAsync()
    {
        if (!_singleInstance.IsPrimaryInstance)
        {
            return false;
        }

        try
        {
            _coordinator = new YpopupCoordinator();
            _windowNavigator = new WindowNavigator(_coordinator);
            _messagePresenter = new IncomingMessagePresenter(_coordinator, _notificationSound);

            _coordinator.PeersChanged += _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _windowNavigator?.RefreshUserListIfOpen());
            _coordinator.MessageReceived += message =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _messagePresenter?.Present(message));

            SetupTrayIcon();

            await _coordinator.StartAsync();

            _awayMonitor = new AwayMonitorService(_coordinator, AwayIdleDetectorFactory.Create());
            _awayMonitor.Start();

            await SharedFolderStartupNotifier.NotifyIfFailedAsync(_coordinator);
            _windowNavigator.ShowUserList();
            return true;
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(
                null,
                "Y-popup",
                $"Y-popup 시작 중 오류가 발생했습니다.\n\n{ex.Message}\n\n" +
                "방화벽에서 UDP/TCP 포트(50505, 50506, 공유폴더 50507) 허용이 필요할 수 있습니다.");
            return false;
        }
    }

    private void SetupTrayIcon()
    {
        _trayIconManager.Show(
            TrayMenuBuilder.DefaultToolTipText,
            TrayMenuBuilder.Create(
                () => _windowNavigator?.ShowUserList(),
                () => Dispatcher.UIThread.Post(() => _ = ShowSettingsAndRefreshAwayAsync()),
                () => Dispatcher.UIThread.Post(() => _ = ShowAboutAsync()),
                App.ShutdownAppAsync),
            () => _windowNavigator?.ShowUserList());
    }

    private async Task ShowSettingsAndRefreshAwayAsync()
    {
        if (_windowNavigator is null)
        {
            return;
        }

        await _windowNavigator.ShowSettingsAsync();
        _awayMonitor?.RefreshAwayStatus();
    }

    private async Task ShowAboutAsync()
    {
        if (_windowNavigator is null)
        {
            return;
        }

        await _windowNavigator.ShowAboutAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_coordinator is not null)
        {
            await _coordinator.DisposeAsync();
            _coordinator = null;
        }

        _awayMonitor?.Dispose();
        _awayMonitor = null;

        _trayIconManager.Dispose();
        _singleInstance.Dispose();
    }
}
