using Avalonia.Controls;
using Ypopup.Desktop.Helpers;
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
        try
        {
            var settingsWindow = new SettingsWindow(_coordinator);
            if (_userListWindow is { IsVisible: true } owner)
            {
                await WindowDialogHelper.ShowDialogAsync(settingsWindow, owner);
            }
            else
            {
                settingsWindow.Show();
                settingsWindow.Activate();
            }

            RefreshUserListIfOpen();
        }
        catch (Exception ex)
        {
            if (_userListWindow is { IsVisible: true } owner)
            {
                await DialogHelper.ShowErrorAsync(owner, "Y-popup", $"설정 창을 열 수 없습니다.\n\n{ex.Message}");
            }
        }
    }

    public async Task ShowAboutAsync()
    {
        var aboutWindow = new AboutWindow();
        if (_userListWindow is { IsVisible: true } owner)
        {
            await WindowDialogHelper.ShowDialogAsync(aboutWindow, owner);
        }
        else
        {
            aboutWindow.Show();
            aboutWindow.Activate();
        }
    }
}
