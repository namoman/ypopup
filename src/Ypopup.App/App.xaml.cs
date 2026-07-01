using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Ypopup.App.Helpers;
using Ypopup.App.Services;
using Ypopup.App.Views;
using Ypopup.Core.Models;
using Ypopup.Network;

namespace Ypopup.App;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static Mutex? _mutex;

    private TaskbarIcon? _trayIcon;
    private YpopupCoordinator? _coordinator;
    private AwayMonitorService? _awayMonitor;
    private UserListWindow? _userListWindow;
    private int _resourcesDisposed;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(true, "Global\\Ypopup-SingleInstance-Mutex", out bool createdNew);
        if (!createdNew)
        {
            var hWnd = FindWindow(null, "Y-popup - 사용자 목록");
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
            Shutdown();
            return;
        }

        try
        {
            _coordinator = new YpopupCoordinator();
            _coordinator.PeersChanged += _ => Dispatcher.Invoke(RefreshUserListIfOpen);
            _coordinator.MessageReceived += message => Dispatcher.Invoke(() => OnMessageReceived(message));

            _trayIcon = new TaskbarIcon
            {
                Icon = IconFactory.CreateTrayIcon(),
                ToolTipText = $"Y-popup - LAN 메신저 ({AppInfo.ContactSummary})",
                ContextMenu = BuildTrayMenu()
            };

            _trayIcon.TrayLeftMouseUp += (_, _) => ShowUserList();
            _trayIcon.TrayMouseDoubleClick += (_, _) => ShowUserList();

            await _coordinator.StartAsync();
            _awayMonitor = new AwayMonitorService(_coordinator);
            _awayMonitor.Start();

            ShowUserList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Y-popup 시작 중 오류가 발생했습니다.\n\n{ex.Message}\n\n방화벽에서 UDP/TCP 포트(50505, 50506) 허용이 필요할 수 있습니다.",
                "Y-popup",
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

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "설정" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var aboutItem = new System.Windows.Controls.MenuItem { Header = "정보" };
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

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
            if (_userListWindow.WindowState == WindowState.Minimized)
            {
                _userListWindow.WindowState = WindowState.Normal;
            }
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
        _awayMonitor?.RefreshAwayStatus();
    }

    private void ShowAbout()
    {
        var aboutWindow = new AboutWindow();
        if (_userListWindow is { IsVisible: true })
        {
            aboutWindow.Owner = _userListWindow;
        }

        aboutWindow.ShowDialog();
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

        if (message.IsAutoReply)
        {
            return;
        }

        if (message.SavedFilePaths.Count > 0)
        {
            NotificationService.PlayFileReceived(_coordinator.Settings);
        }
        else
        {
            NotificationService.PlayMessageReceived(_coordinator.Settings);
        }

        var activeChat = Current.Windows
            .OfType<ComposeWindow>()
            .FirstOrDefault(w => string.Equals(w.RecipientMachineId, message.SenderId, StringComparison.OrdinalIgnoreCase));

        if (activeChat != null)
        {
            activeChat.Activate();
            return;
        }

        var receiveWindow = new ReceiveWindow(_coordinator, message);
        receiveWindow.Show();
        receiveWindow.Activate();

        _trayIcon?.ShowBalloonTip(
            "Y-popup",
            $"{message.SenderName}님의 메시지",
            BalloonIcon.Info);
    }

    public static async Task ShutdownAppAsync()
    {
        var app = (App)Current;
        await app.DisposeResourcesAsync();
        app.Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await DisposeResourcesAsync();
        base.OnExit(e);
    }

    private async Task DisposeResourcesAsync()
    {
        if (Interlocked.Exchange(ref _resourcesDisposed, 1) == 1)
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

        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
