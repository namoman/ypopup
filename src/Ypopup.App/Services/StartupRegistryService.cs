using Microsoft.Win32;

namespace Ypopup.App.Services;

public static class StartupRegistryService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(Core.Models.AppConstants.StartupRegistryValueName) is string;
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

        key.SetValue(Core.Models.AppConstants.StartupRegistryValueName, $"\"{exePath}\"");
    }
}
