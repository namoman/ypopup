using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ypopup.Desktop.ViewModels;

namespace Ypopup.Desktop.Views.Chat;

public partial class ChatRoomWindow : Window
{
    private ChatSessionViewModel? ViewModel => DataContext as ChatSessionViewModel;

    public ChatRoomWindow()
    {
        InitializeComponent();
    }

    public ChatRoomWindow(ChatSessionViewModel viewModel) : this()
    {
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
