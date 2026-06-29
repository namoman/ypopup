using System.Collections.ObjectModel;
using System.Windows;
using Ypopup.App.Helpers;
using Ypopup.Core.Models;
using Ypopup.Network;

namespace Ypopup.App.Views;

public partial class ComposeWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly PeerInfo _recipient;
    private readonly ObservableCollection<string> _attachments = [];

    public ComposeWindow(YpopupCoordinator coordinator, PeerInfo recipient)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _recipient = recipient;

        RecipientTextBlock.Text = $"받는 사람: {recipient.DisplayName} ({recipient.IpAddress})";
        AttachmentListBox.ItemsSource = _attachments;
        MessageFontHelper.Apply(_coordinator.Settings, MessageTextBox);
        UpdateAttachmentSummary();
    }

    private void UpdateAttachmentSummary()
    {
        AttachmentSummaryTextBlock.Text = _attachments.Count == 0
            ? "첨부파일 없음"
            : $"{_attachments.Count}개 파일 첨부됨";
    }

    private void AddAttachments(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && !_attachments.Contains(path))
            {
                _attachments.Add(path);
            }
        }

        UpdateAttachmentSummary();
    }

    private void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "첨부할 파일 선택"
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddAttachments(dialog.FileNames);
        }
    }

    private void MessageTextBox_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void MessageTextBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            AddAttachments(files);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MessageTextBox.Text) && _attachments.Count == 0)
        {
            MessageBox.Show(this, "메시지 또는 첨부파일을 입력하세요.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            await _coordinator.SendMessageAsync(new OutgoingMessage
            {
                Recipient = _recipient,
                Body = MessageTextBox.Text.Trim(),
                AttachmentPaths = _attachments.ToList()
            });

            if (_coordinator.Settings.CloseComposeWindowAfterSend)
            {
                Close();
                return;
            }

            MessageBox.Show(this, "전송되었습니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"전송에 실패했습니다.\n\n{ex.Message}\n\n상대 PC에서 Y-popup이 실행 중인지, 방화벽 설정을 확인하세요.",
                "Y-popup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
