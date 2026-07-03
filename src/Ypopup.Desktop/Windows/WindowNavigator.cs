using Avalonia.Controls;
using Ypopup.Desktop.Views.About;
using Ypopup.Desktop.Views.Settings;
using Ypopup.Desktop.Views.UserList;
using Ypopup.Network;

namespace Ypopup.Desktop.Windows;

/// <summary>앱 창(사용자 목록·설정·정보) 표시를 담당합니다.</summary>
public sealed class WindowNavigator
{
    private readonly YpopupCoordinator _coordinator;
    private UserListWindow? _userListWindow;

    public WindowNavigator(YpopupCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public void RefreshUserListIfOpen()
    {
        _userListWindow?.RefreshPeers();
    }

    public void ShowUserList()
    {
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

    public async Task ShowSettingsAsync()
    {
        var settingsWindow = new SettingsWindow(_coordinator);
        if (_userListWindow is not null)
        {
            await settingsWindow.ShowDialog(_userListWindow);
        }
        else
        {
            settingsWindow.Show();
        }

        RefreshUserListIfOpen();
    }

    public async Task ShowAboutAsync()
    {
        var aboutWindow = new AboutWindow();
        if (_userListWindow is { IsVisible: true })
        {
            await aboutWindow.ShowDialog(_userListWindow);
        }
        else
        {
            aboutWindow.Show();
        }
    }
}
