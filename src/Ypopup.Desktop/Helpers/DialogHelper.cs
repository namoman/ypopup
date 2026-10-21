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

    public static async Task<bool> ShowConfirmAsync(Window owner, string title, string message, string confirmText)
    {
        var confirmed = false;
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            MinWidth = 280,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#F3F5F9"))
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 20, 20, 12),
            Foreground = new SolidColorBrush(Color.Parse("#1E293B"))
        };

        var cancelButton = new Button
        {
            Content = "취소",
            Width = 80,
            IsCancel = true
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Click += (_, _) => dialog.Close();

        var confirmButton = new Button
        {
            Content = confirmText,
            Width = 80,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true
        };
        confirmButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 16),
            Children = { cancelButton, confirmButton }
        };

        dialog.Content = new StackPanel
        {
            Children = { messageBlock, buttons }
        };

        await dialog.ShowDialog(owner);
        return confirmed;
    }

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
            Background = new SolidColorBrush(Color.Parse("#F3F5F9"))
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
