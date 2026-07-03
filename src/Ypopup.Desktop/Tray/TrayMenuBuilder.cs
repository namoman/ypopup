using Avalonia.Controls;
using Ypopup.Core;

namespace Ypopup.Desktop.Tray;

public static class TrayMenuBuilder
{
    public static NativeMenu Create(
        Action showUserList,
        Action showSettings,
        Action showAbout,
        Func<Task> shutdownAsync)
    {
        var menu = new NativeMenu();

        var usersItem = new NativeMenuItem("사용자 목록");
        usersItem.Click += (_, _) => showUserList();
        menu.Items.Add(usersItem);

        var settingsItem = new NativeMenuItem("설정");
        settingsItem.Click += (_, _) => showSettings();
        menu.Items.Add(settingsItem);

        var aboutItem = new NativeMenuItem("정보");
        aboutItem.Click += (_, _) => showAbout();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("종료");
        exitItem.Click += async (_, _) => await shutdownAsync();
        menu.Items.Add(exitItem);

        return menu;
    }

    public static string DefaultToolTipText =>
        $"Y-popup - LAN 메신저 ({AppInfo.ContactSummary})";
}
