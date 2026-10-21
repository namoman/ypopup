using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ypopup.Core.Logging;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.ViewModels;

namespace Ypopup.Desktop.Views.Chat;

public partial class ChatRoomWindow : Window
{
    private readonly Func<Task>? _leaveRoom;
    private ChatSessionViewModel? ViewModel => DataContext as ChatSessionViewModel;

    public ChatRoomWindow()
    {
        InitializeComponent();
    }

    public ChatRoomWindow(ChatSessionViewModel viewModel, Func<Task>? leaveRoom = null) : this()
    {
        _leaveRoom = leaveRoom;
        LeaveButton.IsVisible = leaveRoom is not null;
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            await viewModel.LoadRecentMessagesAsync();
            ScrollToBottom();
        };

        viewModel.Messages.CollectionChanged += (_, _) =>
        {
            ScrollToBottom();
        };
    }

    private void ScrollToBottom()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            MessagesScrollViewer?.ScrollToEnd();
        });
    }

    private async void LeaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_leaveRoom is null)
        {
            return;
        }

        var confirmed = await DialogHelper.ShowConfirmAsync(
            this,
            "단톡방",
            "이 단톡방에서 나갈까요? 나간 뒤에는 이 방의 새 메시지를 받지 않습니다.",
            "나가기");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _leaveRoom();
            Close();
        }
        catch (Exception ex)
        {
            LogService.Error("ChatRoom", $"단톡방 나가기 실패: {ex.Message}");
            await DialogHelper.ShowErrorAsync(this, "단톡방", "단톡방에서 나가지 못했습니다.");
        }
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SendMessageAsync();
        }
    }

    private async void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            if (ViewModel is not null)
            {
                await ViewModel.SendMessageAsync();
            }
        }
    }
}
