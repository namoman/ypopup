using Avalonia.Controls;
using Ypopup.Core.Models;
using Ypopup.Core.Settings;
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

        var tcpResult = SettingsValidator.ValidatePort(networkPanel.TcpPortText, "TCP");
        if (!tcpResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", tcpResult.ErrorMessage);
            return false;
        }

        var discoveryResult = SettingsValidator.ValidatePort(networkPanel.DiscoveryPortText, "UDP");
        if (!discoveryResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", discoveryResult.ErrorMessage);
            return false;
        }

        var tcpPort = int.Parse(networkPanel.TcpPortText);
        var discoveryPort = int.Parse(networkPanel.DiscoveryPortText);

        var portsDifferResult = SettingsValidator.ValidatePortsDiffer(tcpPort, discoveryPort, "TCP", "UDP");
        if (!portsDifferResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", portsDifferResult.ErrorMessage);
            return false;
        }

        var shareFolderEnabled = generalPanel.ShareFolderEnabled;
        var shareFolderPort = AppConstants.DefaultShareFolderPort;
        if (shareFolderEnabled)
        {
            var sfpResult = SettingsValidator.ValidatePort(networkPanel.ShareFolderPortText, "공유폴더 HTTP");
            if (!sfpResult.IsValid)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", sfpResult.ErrorMessage);
                return false;
            }

            shareFolderPort = int.Parse(networkPanel.ShareFolderPortText);

            var sfpDifferTcp = SettingsValidator.ValidatePortsDiffer(shareFolderPort, tcpPort, "공유폴더", "TCP");
            var sfpDifferUdp = SettingsValidator.ValidatePortsDiffer(shareFolderPort, discoveryPort, "공유폴더", "UDP");
            if (!sfpDifferTcp.IsValid || !sfpDifferUdp.IsValid)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", "공유폴더 포트는 TCP/UDP 포트와 다른 번호여야 합니다.");
                return false;
            }

            var pathResult = SettingsValidator.ValidateShareFolderPath(generalPanel.ShareFolderPath);
            if (!pathResult.IsValid)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", pathResult.ErrorMessage);
                return false;
            }
        }

        var awayIdleMinutes = 10;
        if (awayPanel.AwayIdleEnabled)
        {
            var awayResult = SettingsValidator.ValidateAwayIdleMinutes(awayPanel.AwayIdleMinutesText);
            if (!awayResult.IsValid)
            {
                await DialogHelper.ShowWarningAsync(owner, "Y-popup", awayResult.ErrorMessage);
                return false;
            }

            awayIdleMinutes = int.Parse(awayPanel.AwayIdleMinutesText);
        }

        var nameResult = SettingsValidator.ValidateDisplayName(_workingSettings.DisplayName);
        if (!nameResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", nameResult.ErrorMessage);
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
            ? "설정이 저장되었습니다.\n포트/네트워크 변경 사항은 자동으로 적용되었습니다."
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
