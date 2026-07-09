using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ypopup.Core.Models;
using Ypopup.Desktop.Controls;
using Ypopup.Desktop.Helpers;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Compose;

public partial class ComposeWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly PeerInfo _recipient;
    private readonly ObservableCollection<string> _attachments = [];
    private CancellationTokenSource? _sendCts;

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

        ProgressBarControl.CancelRequested += OnCancelRequested;

        DragDrop.SetAllowDrop(this, true);
        this.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        this.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void UpdateAttachmentSummary()
    {
        if (_attachments.Count == 0)
        {
            AttachmentArea.IsVisible = false;
            return;
        }

        AttachmentArea.IsVisible = true;
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

    private async void AttachButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "첨부할 파일 선택",
            AllowMultiple = true
        });

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

        AddAttachments(paths);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
        {
            return;
        }

        var storageFiles = e.Data.GetFiles() ?? [];
        var paths = storageFiles
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

        AddAttachments(paths);
    }

    private void RemoveAttachment_Click(object? sender, RoutedEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is string selectedFile)
        {
            _attachments.Remove(selectedFile);
            UpdateAttachmentSummary();
        }
    }

    private void AttachmentListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && AttachmentListBox.SelectedItem is string selectedFile)
        {
            _attachments.Remove(selectedFile);
            UpdateAttachmentSummary();
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
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

    private void MessageTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            SendButton_Click(sender, e);
        }
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        var body = MessageTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body) && _attachments.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "쪽지 내용 또는 첨부파일을 입력하세요.");
            return;
        }

        var hasAttachments = _attachments.Count > 0;
        if (hasAttachments)
        {
            BeginTransfer();
        }

        SendButton.IsEnabled = false;
        _sendCts = new CancellationTokenSource();

        try
        {
            IProgress<TransferProgress>? progress = hasAttachments
                ? new Progress<TransferProgress>(OnTransferProgress)
                : null;

            await _coordinator.SendMessageAsync(
                new OutgoingMessage
                {
                    Recipient = _recipient,
                    Body = body,
                    AttachmentPaths = _attachments.ToList()
                },
                _sendCts.Token,
                progress);

            EndTransfer();

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
        catch (OperationCanceledException)
        {
            EndTransfer();
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "전송이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            EndTransfer();
            await DialogHelper.ShowErrorAsync(
                this,
                "Y-popup",
                $"전송에 실패했습니다.\n\n{ex.Message}\n\n상대 PC에서 Y-popup이 실행 중인지, 방화벽 설정을 확인하세요.");
        }
        finally
        {
            SendButton.IsEnabled = true;
            _sendCts?.Dispose();
            _sendCts = null;
        }
    }

    private void BeginTransfer()
    {
        ProgressBarControl.Progress = 0;
        ProgressBarControl.FileName = null;
        ProgressArea.IsVisible = true;
    }

    private void EndTransfer()
    {
        ProgressArea.IsVisible = false;
        ProgressBarControl.Progress = 0;
        ProgressBarControl.FileName = null;
    }

    private void OnTransferProgress(TransferProgress p)
    {
        ProgressBarControl.Progress = p.Percent;
        ProgressBarControl.FileName = p.FileName;
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        _sendCts?.Cancel();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _sendCts?.Cancel();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        => WindowDragHelper.OnTitleBarPointerPressed(this, e);
}
