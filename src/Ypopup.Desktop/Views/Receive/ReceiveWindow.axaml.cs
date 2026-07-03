using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ypopup.Core.Models;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Views.Compose;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Receive;

public partial class ReceiveWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly ReceivedMessage _message;
    private readonly PeerInfo _sender;

    public ReceiveWindow(YpopupCoordinator coordinator, ReceivedMessage message)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _message = message;
        _sender = coordinator.GetPeers().FirstOrDefault(peer => peer.MachineId == message.SenderId)
                  ?? new PeerInfo
                  {
                      MachineId = message.SenderId,
                      DisplayName = message.SenderName,
                      IpAddress = message.SenderIpAddress,
                      TcpPort = coordinator.Settings.TcpPort
                  };

        SenderTextBlock.Text = message.IsAutoReply
            ? $"보낸 사람: {message.SenderName} (부재중 자동답장)"
            : $"보낸 사람: {message.SenderName}";
        MessageTextBox.Text = string.IsNullOrWhiteSpace(message.Body) ? "(첨부파일만 전송됨)" : message.Body;
        MessageFontHelper.Apply(_coordinator.Settings, MessageTextBox);
        AttachmentListBox.ItemsSource = message.SavedFilePaths;
        AttachmentListBox.IsVisible = message.SavedFilePaths.Count > 0;
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            e.Handled = true;
            ReplyButton_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseButton_Click(sender, e);
        }
    }

    private void ReplyButton_Click(object? sender, RoutedEventArgs e)
    {
        var composeWindow = new ComposeWindow(_coordinator, _sender);
        composeWindow.Show();
        composeWindow.Activate();

        if (_coordinator.Settings.CloseReceiveWindowOnReply)
        {
            Close();
        }
    }

    private void AttachmentListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is string path && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    private void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
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

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        => WindowDragHelper.OnTitleBarPointerPressed(this, e);
}
