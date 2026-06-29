using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Ypopup.Core.Models;

namespace Ypopup.App.Services;

public enum FirewallRuleStatus
{
    Unknown,
    NotConfigured,
    Configured
}

public sealed record FirewallStatus(
    FirewallRuleStatus RuleStatus,
    bool UdpPortOpen,
    bool TcpPortOpen,
    string ExecutablePath);

public static class FirewallHelper
{
    private const string AppRuleName = "Y-popup";
    private const string UdpRuleName = "Y-popup UDP";
    private const string TcpRuleName = "Y-popup TCP";

    public static FirewallStatus GetStatus(AppSettings settings)
    {
        var exePath = GetExecutablePath();
        var ruleStatus = HasConfiguredRules()
            ? FirewallRuleStatus.Configured
            : FirewallRuleStatus.NotConfigured;

        return new FirewallStatus(
            ruleStatus,
            IsUdpPortOpen(settings.DiscoveryPort),
            IsTcpPortOpen(settings.TcpPort),
            exePath);
    }

    public static string GetStatusSummary(FirewallStatus status, AppSettings settings)
    {
        var ruleText = status.RuleStatus switch
        {
            FirewallRuleStatus.Configured => "방화벽 규칙: 등록됨",
            FirewallRuleStatus.NotConfigured => "방화벽 규칙: 미등록",
            _ => "방화벽 규칙: 확인 불가"
        };

        var udpText = status.UdpPortOpen ? "수신 가능" : "차단 또는 사용 불가";
        var tcpText = status.TcpPortOpen ? "수신 가능" : "차단 또는 사용 불가";

        return $"{ruleText}\nUDP {settings.DiscoveryPort}: {udpText}\nTCP {settings.TcpPort}: {tcpText}";
    }

    public static bool TryAddFirewallRules(AppSettings settings, out string message)
    {
        var exePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            message = "실행 파일 경로를 확인할 수 없습니다.";
            return false;
        }

        var commands = new[]
        {
            $"advfirewall firewall delete rule name=\"{AppRuleName}\"",
            $"advfirewall firewall delete rule name=\"{UdpRuleName}\"",
            $"advfirewall firewall delete rule name=\"{TcpRuleName}\"",
            BuildAddProgramRuleCommand(AppRuleName, exePath),
            BuildAddPortRuleCommand(UdpRuleName, "UDP", settings.DiscoveryPort),
            BuildAddPortRuleCommand(TcpRuleName, "TCP", settings.TcpPort)
        };

        foreach (var command in commands)
        {
            var allowFailure = command.Contains("delete rule", StringComparison.Ordinal);
            if (!TryRunElevatedNetsh(command, out var error, allowFailure))
            {
                if (IsUserCancelled(error))
                {
                    message = "관리자 권한 요청이 취소되었습니다.";
                    return false;
                }

                message = $"방화벽 규칙 추가에 실패했습니다.\n\n{error}";
                return false;
            }
        }

        message = "방화벽 허용 규칙이 추가되었습니다.";
        return true;
    }

    public static void OpenWindowsFirewallSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("firewall.cpl") { UseShellExecute = true });
        }
        catch (Exception)
        {
            Process.Start(new ProcessStartInfo("ms-settings:windowsdefender-firewall") { UseShellExecute = true });
        }
    }

    private static string GetExecutablePath()
    {
        try
        {
            return Environment.ProcessPath ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool HasConfiguredRules()
    {
        return RuleExists(AppRuleName) || RuleExists(UdpRuleName) || RuleExists(TcpRuleName);
    }

    private static bool RuleExists(string ruleName)
    {
        var output = RunNetsh($"advfirewall firewall show rule name=\"{ruleName}\"");
        return output.Contains("Ok.", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAddProgramRuleCommand(string ruleName, string exePath)
    {
        var escapedPath = exePath.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program=\"{escapedPath}\" enable=yes profile=private,domain,public";
    }

    private static string BuildAddPortRuleCommand(string ruleName, string protocol, int port)
    {
        return $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol={protocol} localport={port} enable=yes profile=private,domain,public";
    }

    private static bool TryRunElevatedNetsh(string arguments, out string error, bool allowFailure = false)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                error = "관리자 권한 프로세스를 시작할 수 없습니다.";
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode == 0 || allowFailure)
            {
                error = string.Empty;
                return true;
            }

            error = $"netsh 종료 코드: {process.ExitCode}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsUserCancelled(string error)
    {
        return error.Contains("canceled", StringComparison.OrdinalIgnoreCase)
               || error.Contains("취소", StringComparison.OrdinalIgnoreCase)
               || error.Contains("1223", StringComparison.Ordinal);
    }

    private static string RunNetsh(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool IsUdpPortOpen(int port)
    {
        try
        {
            using var client = new UdpClient(port);
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsTcpPortOpen(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
