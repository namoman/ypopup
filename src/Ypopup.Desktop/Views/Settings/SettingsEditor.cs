using Avalonia.Controls;
using Ypopup.Core.Models;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Platform.Firewall;
using Ypopup.Desktop.Platform.Startup;
using Ypopup.Desktop.Views.Settings.Panels;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Settings;

public sealed class SettingsEditor
{
    private readonly YpopupCoordinator _coordinator;
    private readonly IFirewallService _firewallService;
    private readonly IStartupService _startupService;
    private AppSettings _workingSettings = new();
    private AppSettings _originalSettings = new();

    public SettingsEditor(YpopupCoordinator coordinator)
    {
        _coordinator = coordinator;
        _firewallService = FirewallServiceFactory.Create();
        _startupService = StartupServiceFactory.Create();
    }

    public AppSettings WorkingSettings => _workingSettings;

    public void LoadIntoPanels(
        ProfileSettingsPanel profilePanel,
        NetworkSettingsPanel networkPanel,
        GeneralSettingsPanel generalPanel,
        AwaySettingsPanel awayPanel)
    {
        _workingSettings = CloneSettings(_coordinator.Settings);
        _originalSettings = CloneSettings(_coordinator.Settings);

        profilePanel.Load(_workingSettings);
        generalPanel.Initialize(_startupService);
        generalPanel.Load(_workingSettings);
        awayPanel.Load(_workingSettings);

        networkPanel.Initialize(_firewallService, _coordinator, () => BuildFirewallSettingsPreview(networkPanel, generalPanel));
        networkPanel.Load(_workingSettings);
    }

    public AppSettings BuildFirewallSettingsPreview(
        NetworkSettingsPanel networkPanel,
        GeneralSettingsPanel generalPanel)
    {
        var preview = CloneSettings(_workingSettings);
        networkPanel.ApplyTo(preview);
        generalPanel.ApplyTo(preview);
        return preview;
    }

    public void SyncWorkingSettingsFromPanels(
        ProfileSettingsPanel profilePanel,
        NetworkSettingsPanel networkPanel,
        GeneralSettingsPanel generalPanel,
        AwaySettingsPanel awayPanel)
    {
        profilePanel.ApplyTo(_workingSettings);
        networkPanel.ApplyTo(_workingSettings);
        generalPanel.ApplyTo(_workingSettings);
        awayPanel.ApplyTo(_workingSettings);
    }

    public async Task<bool> TrySaveAsync(
        Window owner,
        ProfileSettingsPanel profilePanel,
        NetworkSettingsPanel networkPanel,
        GeneralSettingsPanel generalPanel,
        AwaySettingsPanel awayPanel)
    {
        SyncWorkingSettingsFromPanels(profilePanel, networkPanel, generalPanel, awayPanel);

        if (!int.TryParse(networkPanel.TcpPortText, out var tcpPort) || tcpPort is < 1024 or > 65535)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", "TCP 포트는 1024~65535 사이여야 합니다.");
            return false;
        }

        if (!int.TryParse(networkPanel.DiscoveryPortText, out var discoveryPort) || discoveryPort is < 1024 or > 65535)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", "UDP 포트는 1024~65535 사이여야 합니다.");
            return false;
        }

        if (tcpPort == discoveryPort)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", "TCP 포트와 UDP 포트는 다른 번호여야 합니다.");
            return false;
        }

        var shareFolderEnabled = generalPanel.ShareFolderEnabled;
        var shareFolderPort = AppConstants.DefaultShareFolderPort;
        if (shareFolderEnabled)
        {
            if (!int.TryParse(networkPanel.ShareFolderPortText, out shareFolderPort) || shareFolderPort is < 1024 or > 65535)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", "공유폴더 HTTP 포트는 1024~65535 사이여야 합니다.");
                return false;
            }

            if (shareFolderPort == tcpPort || shareFolderPort == discoveryPort)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", "공유폴더 포트는 TCP/UDP 포트와 다른 번호여야 합니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(generalPanel.ShareFolderPath))
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", "공유폴더 경로를 입력하세요.");
                return false;
            }
        }

        var awayIdleMinutes = 10;
        if (awayPanel.AwayIdleEnabled)
        {
            if (!int.TryParse(awayPanel.AwayIdleMinutesText, out awayIdleMinutes) || awayIdleMinutes < 1)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", "부재 유휴 시간은 1분 이상이어야 합니다.");
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(_workingSettings.DisplayName))
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", "표시 이름을 입력하세요.");
            return false;
        }

        _workingSettings.TcpPort = tcpPort;
        _workingSettings.DiscoveryPort = discoveryPort;
        _workingSettings.ShareFolderPort = shareFolderPort;
        _workingSettings.ShareFolderEnabled = shareFolderEnabled;
        if (awayPanel.AwayIdleEnabled)
        {
            _workingSettings.AwayIdleMinutes = awayIdleMinutes;
        }

        try
        {
            _startupService.SetEnabled(generalPanel.RunAtStartupEnabled);
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", $"시작 프로그램 등록에 실패했습니다.\n\n{ex.Message}");
            return false;
        }

        var requiresRestart = tcpPort != _originalSettings.TcpPort
                            || discoveryPort != _originalSettings.DiscoveryPort
                            || shareFolderPort != _originalSettings.ShareFolderPort;

        _coordinator.SaveSettings(_workingSettings);

        if (_workingSettings.ShareFolderEnabled && !_coordinator.SharedFolderHostStatus.IsRunning)
        {
            await DialogHelper.ShowWarningAsync(
                owner,
                "Y-popup",
                $"공유폴더 서버를 시작하지 못했습니다.\n\n{_coordinator.SharedFolderHostStatus.ErrorMessage}");
        }

        var message = requiresRestart
            ? "설정이 저장되었습니다.\n포트 변경은 프로그램 재시작 후 적용됩니다."
            : "설정이 저장되었습니다.";

        await DialogHelper.ShowInfoAsync(owner, "Y-popup", message);
        return true;
    }

    public static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            MachineId = source.MachineId,
            DisplayName = source.DisplayName,
            PreferredLocalIp = source.PreferredLocalIp,
            Group = source.Group,
            OnlySameGroup = source.OnlySameGroup,
            Email = source.Email,
            Memo = source.Memo,
            KeepWindowTopmost = source.KeepWindowTopmost,
            CloseComposeWindowAfterSend = source.CloseComposeWindowAfterSend,
            CloseReceiveWindowOnReply = source.CloseReceiveWindowOnReply,
            SoundEnabled = source.SoundEnabled,
            PlayMessageReceivedSound = source.PlayMessageReceivedSound,
            PlayFileReceivedSound = source.PlayFileReceivedSound,
            MessageFontFamily = source.MessageFontFamily,
            MessageFontSize = source.MessageFontSize,
            ReceiveDirectory = source.ReceiveDirectory,
            TcpPort = source.TcpPort,
            DiscoveryPort = source.DiscoveryPort,
            AwayEnabledByIdle = source.AwayEnabledByIdle,
            AwayIdleMinutes = source.AwayIdleMinutes,
            AwayMessage = source.AwayMessage,
            ShareFolderEnabled = source.ShareFolderEnabled,
            ShareFolderPath = source.ShareFolderPath,
            ShareFolderPort = source.ShareFolderPort
        };
    }
}
