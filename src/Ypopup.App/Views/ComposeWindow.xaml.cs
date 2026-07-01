using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ypopup.App.Helpers;
using Ypopup.Core.Models;
using Ypopup.Network;

namespace Ypopup.App.Views;

public class ChatMessageViewModel
{
    public required string SenderName { get; set; }
    public required string Body { get; set; }
    public required string TimeText { get; set; }
    public required bool IsMe { get; set; }
    public required List<string> Attachments { get; set; }

    public System.Windows.HorizontalAlignment HorizontalAlignment => IsMe ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
    public string BubbleBackground => IsMe ? "#ef4444" : "#1E293B"; // Use red for Me, dark navy for Peer
    public Visibility AttachmentVisibility => (Attachments != null && Attachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
}

public partial class ComposeWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly PeerInfo _recipient;
    private readonly ObservableCollection<string> _attachments = [];
    private readonly ObservableCollection<ChatMessageViewModel> _chatLog = [];

    public string RecipientMachineId => _recipient.MachineId;

    public ComposeWindow(YpopupCoordinator coordinator, PeerInfo recipient)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _recipient = recipient;
        Topmost = _coordinator.Settings.KeepWindowTopmost;

        TitleTextBlock.Text = $"Chat: {recipient.DisplayName} ({recipient.IpAddress})";
        AttachmentListBox.ItemsSource = _attachments;
        ChatLogListBox.ItemsSource = _chatLog;
        MessageFontHelper.Apply(_coordinator.Settings, MessageTextBox);
        UpdateAttachmentSummary();

        // Subscribe to real-time incoming messages to append directly
        _coordinator.MessageReceived += OnMessageReceived;
        Closed += (s, e) => _coordinator.MessageReceived -= OnMessageReceived;
    }

    private void OnMessageReceived(ReceivedMessage message)
    {
        if (message.SenderId == _recipient.MachineId)
        {
            Dispatcher.Invoke(() =>
            {
                _chatLog.Add(new ChatMessageViewModel
                {
                    SenderName = message.SenderName,
                    Body = string.IsNullOrWhiteSpace(message.Body) ? "(첨부파일 전송됨)" : message.Body,
                    TimeText = DateTime.Now.ToString("t"),
                    IsMe = false,
                    Attachments = message.SavedFilePaths.Select(System.IO.Path.GetFileName).ToList()!
                });

                ScrollToBottom();
            });
        }
    }

    private void ScrollToBottom()
    {
        if (ChatLogListBox.Items.Count > 0)
        {
            ChatLogListBox.ScrollIntoView(ChatLogListBox.Items[^1]);
        }
    }

    private void UpdateAttachmentSummary()
    {
        if (_attachments.Count == 0)
        {
            AttachmentArea.Visibility = Visibility.Collapsed;
        }
        else
        {
            AttachmentArea.Visibility = Visibility.Visible;
            AttachmentSummaryTextBlock.Text = $"{_attachments.Count}개 파일";
        }
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

    private void Window_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
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

    private void AttachmentListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete)
        {
            if (AttachmentListBox.SelectedItem is string selectedFile)
            {
                _attachments.Remove(selectedFile);
                UpdateAttachmentSummary();
            }
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var body = MessageTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(body) && _attachments.Count == 0)
        {
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            var attachmentsSnapshot = _attachments.ToList();

            await _coordinator.SendMessageAsync(new OutgoingMessage
            {
                Recipient = _recipient,
                Body = body,
                AttachmentPaths = attachmentsSnapshot
            });

            // Append sent message to chat log bubble list
            _chatLog.Add(new ChatMessageViewModel
            {
                SenderName = _coordinator.Settings.DisplayName,
                Body = string.IsNullOrWhiteSpace(body) ? "(첨부파일만 전송됨)" : body,
                TimeText = DateTime.Now.ToString("t"),
                IsMe = true,
                Attachments = attachmentsSnapshot.Select(Path.GetFileName).ToList()!
            });

            // Reset input fields
            MessageTextBox.Text = string.Empty;
            _attachments.Clear();
            UpdateAttachmentSummary();

            ScrollToBottom();
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void AttachmentFile_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
        {
            var receiveDir = _coordinator.Settings.ReceiveDirectory;
            var fullPath = Path.Combine(receiveDir, tb.Text);
            if (File.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
            }
            else
            {
                if (Directory.Exists(receiveDir))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(receiveDir) { UseShellExecute = true });
                }
            }
        }
    }
}
