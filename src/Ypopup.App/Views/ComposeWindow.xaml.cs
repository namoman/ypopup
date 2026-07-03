using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Ypopup.App.Helpers;
using Ypopup.Core.Models;
using Ypopup.Network;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

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
        Topmost = _coordinator.Settings.KeepWindowTopmost;

        RecipientTextBlock.Text = $"받는 사람: {recipient.DisplayName}  ({recipient.IpAddress})";
        AttachmentListBox.ItemsSource = _attachments;
        MessageFontHelper.Apply(_coordinator.Settings, MessageTextBox);
        UpdateAttachmentSummary();
    }

    private void UpdateAttachmentSummary()
    {
        if (_attachments.Count == 0)
        {
            AttachmentArea.Visibility = Visibility.Collapsed;
            return;
        }

        AttachmentArea.Visibility = Visibility.Visible;
        AttachmentSummaryTextBlock.Text = $"📎 첨부 {_attachments.Count}개";
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

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            AddAttachments(files);
        }
    }

    private void MessageTextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void MessageTextBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            AddAttachments(files);
        }
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is string selectedFile)
        {
            _attachments.Remove(selectedFile);
            UpdateAttachmentSummary();
        }
    }

    private void AttachmentListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && AttachmentListBox.SelectedItem is string selectedFile)
        {
            _attachments.Remove(selectedFile);
            UpdateAttachmentSummary();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            e.Handled = true;
            SendButton_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseButton_Click(sender, e);
        }
    }

    private void MessageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SendButton_Click(sender, e);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var body = MessageTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(body) && _attachments.Count == 0)
        {
            MessageBox.Show(this, "쪽지 내용 또는 첨부파일을 입력하세요.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            await _coordinator.SendMessageAsync(new OutgoingMessage
            {
                Recipient = _recipient,
                Body = body,
                AttachmentPaths = _attachments.ToList()
            });

            if (_coordinator.Settings.CloseComposeWindowAfterSend)
            {
                Close();
                return;
            }

            MessageTextBox.Text = string.Empty;
            _attachments.Clear();
            UpdateAttachmentSummary();
            MessageTextBox.Focus();
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
