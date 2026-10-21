using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core;
using Ypopup.Core.Logging;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class AboutSettingsPanel : UserControl
{
    public AboutSettingsPanel()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"v{AppInfo.Version}";
    }

    private void EmailLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink($"mailto:{AppInfo.Email}");

    private void WebsiteLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink(AppInfo.Website);

    private void RepositoryLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink(AppInfo.Repository);

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
