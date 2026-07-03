using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core;

namespace Ypopup.Desktop.Views.About;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = AppInfo.VersionDisplay;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();

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
