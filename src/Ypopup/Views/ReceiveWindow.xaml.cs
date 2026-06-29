using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Ypopup.Models;

namespace Ypopup.Views;

public partial class ReceiveWindow : Window
{
    private readonly ReceivedMessage _message;

    public ReceiveWindow(ReceivedMessage message)
    {
        InitializeComponent();
        _message = message;

        SenderTextBlock.Text = $"보낸 사람: {message.SenderName}";
        MessageTextBox.Text = string.IsNullOrWhiteSpace(message.Body) ? "(첨부파일만 전송됨)" : message.Body;
        AttachmentListBox.ItemsSource = message.SavedFilePaths;
        AttachmentListBox.Visibility = message.SavedFilePaths.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AttachmentListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is string path && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_message.SavedFilePaths.Count == 0)
        {
            return;
        }

        var folder = Path.GetDirectoryName(_message.SavedFilePaths[0]);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
