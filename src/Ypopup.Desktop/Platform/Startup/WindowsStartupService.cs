using Microsoft.Win32;
using Ypopup.Core.Models;
using Ypopup.Core.Startup;

namespace Ypopup.Desktop.Platform.Startup;

public sealed class WindowsStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppConstants.StartupRegistryValueName) is string;
    }

    /// <summary>예전 등록(인자 없음)을 트레이 전용 실행으로 갱신합니다.</summary>
    public void EnsureTrayLaunchRegistered()
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

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            key.DeleteValue(AppConstants.StartupRegistryValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");

        key.SetValue(AppConstants.StartupRegistryValueName, $"\"{exePath}\" {StartupLaunchOptions.TrayOnlyFlag}");
    }
}
