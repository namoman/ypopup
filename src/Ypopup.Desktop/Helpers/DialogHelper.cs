using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ypopup.Desktop.Helpers;

public static class DialogHelper
{
    public static Task ShowInfoAsync(Window? owner, string title, string message)
        => ShowAsync(owner, title, message);

    public static Task ShowWarningAsync(Window? owner, string title, string message)
        => ShowAsync(owner, title, message);

    public static Task ShowErrorAsync(Window? owner, string title, string message)
        => ShowAsync(owner, title, message);

    private static async Task ShowAsync(Window? owner, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            MinWidth = 280,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#F8FAFC"))
        };
        dialog.Content = BuildContent(message, () => dialog.Close());

        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
            await Task.CompletedTask;
        }
    }

    private static Control BuildContent(string message, Action onOk)
    {
        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 20, 20, 12),
            Foreground = new SolidColorBrush(Color.Parse("#1E293B"))
        };

        var okButton = new Button
        {
            Content = "확인",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
            IsDefault = true
        };
        okButton.Click += (_, _) => onOk();

        return new StackPanel
        {
            Children = { messageBlock, okButton }
        };
    }
}
