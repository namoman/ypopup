using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Desktop.Controls;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Views.Chat;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Compose;

public enum ComposeSendMode
{
    Direct,
    Notice
}

public partial class ComposeWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly ObservableCollection<PeerInfo> _recipients = [];
    private readonly ObservableCollection<string> _attachments = [];
    private readonly ComposeSendMode _mode;
    private CancellationTokenSource? _sendCts;

    public ComposeWindow(YpopupCoordinator coordinator, PeerInfo recipient)
        : this(coordinator, [recipient], ComposeSendMode.Direct)
    {
    }

    public ComposeWindow(YpopupCoordinator coordinator, IReadOnlyList<PeerInfo> recipients, ComposeSendMode mode)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _mode = mode;
        Topmost = _coordinator.Settings.KeepWindowTopmost;

        foreach (var peer in recipients)
        {
            AddRecipient(peer);
        }

        RecipientList.ItemsSource = _recipients;
        AttachmentListBox.ItemsSource = _attachments;
        MessageFontHelper.Apply(_coordinator.Settings, MessageTextBox);
        UpdateAttachmentSummary();

        if (_mode == ComposeSendMode.Notice)
        {
            HeaderTitleTextBlock.Text = "단체 쪽지";
            Title = "Y-popup - 단체 쪽지";
            AttachButton.IsVisible = false;
        }

        ProgressBarControl.CancelRequested += OnCancelRequested;

        DragDrop.SetAllowDrop(this, true);
        this.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        this.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void AddRecipient(PeerInfo peer)
    {
        if (_recipients.Any(item => string.Equals(item.MachineId, peer.MachineId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _recipients.Add(peer);
    }

    private async void AddRecipient_Click(object? sender, RoutedEventArgs e)
    {
        var candidates = _coordinator.GetPeers()
            .Where(peer => _recipients.All(item =>
                !string.Equals(item.MachineId, peer.MachineId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "더 추가할 LAN 사용자가 없습니다.");
            return;
        }

        var dialog = new CreateRoomDialog(candidates, pickOnly: true);
        await dialog.ShowDialog(this);
        if (dialog.Result is null)
        {
            return;
        }

        foreach (var peer in dialog.Result.SelectedPeers)
        {
            AddRecipient(peer);
        }
    }

    private void RemoveRecipient_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PeerInfo peer })
        {
            _recipients.Remove(peer);
        }
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
        if (_recipients.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "받는 사람을 한 명 이상 선택하세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(body) && _attachments.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "쪽지 내용 또는 첨부파일을 입력하세요.");
            return;
        }

        var hasAttachments = _mode == ComposeSendMode.Direct && _attachments.Count > 0;
        if (hasAttachments)
        {
            BeginTransfer();
        }

        SendButton.IsEnabled = false;
        _sendCts = new CancellationTokenSource();

        try
        {
            var failed = await SendToRecipientsAsync(body, hasAttachments, _sendCts.Token);
            EndTransfer();

            if (failed.Count > 0)
            {
                LogService.Warning("Compose", $"동보 전송 일부 실패: {failed.Count}/{_recipients.Count}");
                await DialogHelper.ShowErrorAsync(
                    this,
                    "Y-popup",
                    $"일부 전송에 실패했습니다.\n\n{string.Join("\n", failed)}");
                return;
            }

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
            LogService.Error("Compose", $"전송 실패: {ex.Message}");
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

    private async Task<List<string>> SendToRecipientsAsync(string body, bool hasAttachments, CancellationToken cancellationToken)
    {
        var failed = new List<string>();
        var targets = _recipients.ToList();

        if (_mode == ComposeSendMode.Notice)
        {
            var results = await _coordinator.SendMulticastNoticeAsync(body, targets, cancellationToken);
            failed.AddRange(results.Where(result => !result.IsSuccess).Select(result =>
            {
                var name = targets.FirstOrDefault(peer =>
                    string.Equals(peer.MachineId, result.PeerId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                    ?? result.PeerId;
                return $"{name}: {result.ErrorMessage}";
            }));
            return failed;
        }

        IProgress<TransferProgress>? progress = hasAttachments
            ? new Progress<TransferProgress>(OnTransferProgress)
            : null;
        var attachments = _attachments.ToList();

        for (var index = 0; index < targets.Count; index++)
        {
            var peer = targets[index];
            if (targets.Count > 1)
            {
                ProgressBarControl.FileName = $"{peer.DisplayName} ({index + 1}/{targets.Count})";
            }

            try
            {
                await _coordinator.SendMessageAsync(
                    new OutgoingMessage
                    {
                        Recipient = peer,
                        Body = body,
                        AttachmentPaths = attachments
                    },
                    cancellationToken,
                    progress);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogService.Warning("Compose", $"{peer.DisplayName} 전송 실패: {ex.Message}");
                failed.Add($"{peer.DisplayName}: {ex.Message}");
            }
        }

        return failed;
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
