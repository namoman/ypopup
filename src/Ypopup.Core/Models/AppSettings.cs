namespace Ypopup.Core.Models;

public static class AppConstants
{
    public const string ProductName = "Y-popup";
    public const string AppFolderName = "Y-popup";
    public const string StartupRegistryValueName = "Y-popup";
    public const int DefaultTcpPort = 50506;
    public const int DefaultDiscoveryPort = 50505;
}

/// <summary>
/// Y-popup 설정. UDP Announce / TCP 메시지 프로토콜과 1:1 대응되는 항목 위주.
/// </summary>
public sealed class AppSettings
{
    public string MachineId { get; set; } = Guid.NewGuid().ToString();

    // UDP Announce 프로필
    public string DisplayName { get; set; } = Environment.UserName;
    public string Group { get; set; } = string.Empty;
    public string Memo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // 네트워크
    public string PreferredLocalIp { get; set; } = string.Empty;
    public int DiscoveryPort { get; set; } = AppConstants.DefaultDiscoveryPort;
    public int TcpPort { get; set; } = AppConstants.DefaultTcpPort;
    public bool OnlySameGroup { get; set; }

    // UI · 동작
    public bool KeepWindowTopmost { get; set; } = true;
    public bool CloseComposeWindowAfterSend { get; set; } = true;
    public bool CloseReceiveWindowOnReply { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool PlayMessageReceivedSound { get; set; } = true;
    public bool PlayFileReceivedSound { get; set; } = true;
    public string MessageFontFamily { get; set; } = "Segoe UI";
    public double MessageFontSize { get; set; } = 13;
    public string ReceiveDirectory { get; set; } = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
        "down");

    // 부재 (Announce.IsAway + TCP 자동답장)
    public bool AwayEnabledByIdle { get; set; }
    public int AwayIdleMinutes { get; set; } = 10;
    public string AwayMessage { get; set; } = "지금은 부재중입니다. 나중에 다시 연락해 주세요.";

    // 하위 호환 (구 settings.json)
    public bool PlayNotificationSound
    {
        get => SoundEnabled && PlayMessageReceivedSound;
        set
        {
            SoundEnabled = value;
            PlayMessageReceivedSound = value;
        }
    }

    [Obsolete("Y-popup v2 설정에서는 사용하지 않습니다.")]
    public bool AwayEnabledByMouseZone { get; set; }

    [Obsolete("Y-popup v2 설정에서는 사용하지 않습니다.")]
    public int AwayMouseZone { get; set; }
}
