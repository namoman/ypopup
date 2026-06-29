using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Ypopup.Helpers;
using Ypopup.Models;
using Ypopup.Services;
using Ypopup.Views;

namespace Ypopup;

public partial class App : System.Windows.Application
{
    private TaskbarIcon? _trayIcon;
    private YpopupCoordinator? _coordinator;
    private UserListWindow? _userListWindow;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        _coordinator = new YpopupCoordinator();
        _coordinator.PeersChanged += _ => Dispatcher.Invoke(RefreshUserListIfOpen);
        _coordinator.MessageReceived += message => Dispatcher.Invoke(() => OnMessageReceived(message));

        _trayIcon = new TaskbarIcon
        {
            IconSource = IconFactory.CreateTrayIcon(),
            ToolTipText = "Ypopup - LAN 메신저",
            ContextMenu = BuildTrayMenu()
        };

        _trayIcon.TrayLeftMouseUp += (_, _) => ShowUserList();
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowUserList();

        try
        {
            await _coordinator.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ypopup 시작 중 오류가 발생했습니다.\n\n{ex.Message}\n\n방화벽에서 UDP/TCP 포트(50505, 50506) 허용이 필요할 수 있습니다.",
                "Ypopup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private System.Windows.Controls.ContextMenu BuildTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var usersItem = new System.Windows.Controls.MenuItem { Header = "사용자 목록" };
        usersItem.Click += (_, _) => ShowUserList();
        menu.Items.Add(usersItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "환경 설정" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "종료" };
        exitItem.Click += async (_, _) => await ShutdownAppAsync();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowUserList()
    {
        if (_coordinator is null)
        {
            return;
        }

        if (_userListWindow is { IsVisible: true })
        {
            _userListWindow.Activate();
            return;
        }

        _userListWindow = new UserListWindow(_coordinator);
        _userListWindow.Closed += (_, _) => _userListWindow = null;
        _userListWindow.Show();
        _userListWindow.Activate();
    }

    private void ShowSettings()
    {
        if (_coordinator is null)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_coordinator);
        settingsWindow.Owner = _userListWindow;
        settingsWindow.ShowDialog();
    }

    private void RefreshUserListIfOpen()
    {
        _userListWindow?.RefreshPeers();
    }

    private void OnMessageReceived(ReceivedMessage message)
    {
        if (_coordinator is null)
        {
            return;
        }

        NotificationService.PlayNotificationSound(_coordinator.Settings.PlayNotificationSound);

        var receiveWindow = new ReceiveWindow(message);
        receiveWindow.Show();
        receiveWindow.Activate();

        _trayIcon?.ShowBalloonTip(
            "Ypopup",
            $"{message.SenderName}님의 메시지",
            BalloonIcon.Info);
    }

    public static async Task ShutdownAppAsync()
    {
        var app = (App)Current;
        if (app._coordinator is not null)
        {
            await app._coordinator.DisposeAsync();
        }

        app._trayIcon?.Dispose();
        app.Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_coordinator is not null)
        {
            await _coordinator.DisposeAsync();
        }

        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
