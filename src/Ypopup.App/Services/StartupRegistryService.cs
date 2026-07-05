using Microsoft.Win32;
using Ypopup.Core.Models;
using Ypopup.Core.Startup;

namespace Ypopup.App.Services;

public static class StartupRegistryService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppConstants.StartupRegistryValueName) is string;
    }

    public static void EnsureTrayLaunchRegistered()
    {
        if (!IsEnabled())
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key?.GetValue(AppConstants.StartupRegistryValueName) is not string current
            || current.Contains(StartupLaunchOptions.TrayOnlyFlag, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetEnabled(true);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            key.DeleteValue(Core.Models.AppConstants.StartupRegistryValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
        }

        key.SetValue(AppConstants.StartupRegistryValueName, $"\"{exePath}\" {StartupLaunchOptions.TrayOnlyFlag}");
    }
}
