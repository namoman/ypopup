using Microsoft.Win32;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Platform.Startup;

public sealed class WindowsStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppConstants.StartupRegistryValueName) is string;
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

        key.SetValue(AppConstants.StartupRegistryValueName, $"\"{exePath}\"");
    }
}
