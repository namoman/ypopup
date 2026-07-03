using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class AboutSettingsPanel : UserControl
{
    public AboutSettingsPanel()
    {
        InitializeComponent();
        VersionTextBlock.Text = AppInfo.VersionDisplay;
    }

    private void EmailLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink($"mailto:{AppInfo.Email}");

    private void WebsiteLink_Click(object? sender, RoutedEventArgs e)
        => OpenLink(AppInfo.Website);

    private static void OpenLink(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open link: {ex.Message}");
        }
    }
}
