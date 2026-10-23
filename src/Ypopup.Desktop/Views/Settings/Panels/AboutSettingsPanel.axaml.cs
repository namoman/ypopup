using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ypopup.Core;
using Ypopup.Core.Logging;
using Ypopup.Core.Updates;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class AboutSettingsPanel : UserControl
{
    private static readonly HttpClient UpdateHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    private UpdateOffer? _offer;
    private int _checking;

    public AboutSettingsPanel()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"v{AppInfo.Version}";
    }

    private void AuthorLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink(AppInfo.AuthorWebsite);

    private void EmailLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink($"mailto:{AppInfo.Email}");

    private void WebsiteLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink(AppInfo.Website);

    private void RepositoryLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink(AppInfo.Repository);

    private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (Interlocked.Exchange(ref _checking, 1) == 1)
        {
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        DownloadUpdateButton.IsVisible = false;
        _offer = null;
        UpdateStatusText.Text = "확인 중…";

        try
        {
            var result = await GitHubUpdateChecker.CheckAsync(
                UpdateHttp,
                AppInfo.Version,
                GitHubUpdateChecker.CurrentPlatform());

            await Dispatcher.UIThread.InvokeAsync(() => ShowUpdateResult(result));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogService.Warning("AboutSettings", $"Update check: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateStatusText.Text = "업데이트를 확인하지 못했습니다.";
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => CheckUpdateButton.IsEnabled = true);
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    private void DownloadUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_offer is null)
        {
            return;
        }

        OpenLink(_offer.DownloadUrl);
    }

    private void ShowUpdateResult(UpdateCheckResult result)
    {
        if (result.Offer is { } offer)
        {
            _offer = offer;
            DownloadUpdateButton.Content = offer.FileName is null ? "릴리스 열기" : "받기";
            DownloadUpdateButton.IsVisible = true;
            UpdateStatusText.Text = offer.FileName is null
                ? $"v{offer.LatestVersion}이 있습니다. 릴리스 페이지에서 받을 수 있습니다."
                : $"v{offer.LatestVersion}이 있습니다. {offer.FileName}";
            return;
        }

        _offer = null;
        DownloadUpdateButton.IsVisible = false;
        UpdateStatusText.Text = result.Failure ?? "현재 최신 버전입니다.";
    }

    private static void OpenLink(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Warning("AboutSettings", $"Open link: {ex.Message}");
        }
    }
}
