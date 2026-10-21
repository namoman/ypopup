using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Views.UserList;

public partial class PeerProfileWindow : Window
{
    private Bitmap? _avatar;

    public PeerProfileWindow()
    {
        InitializeComponent();
    }

    public static Task ShowAsync(Window owner, PeerInfo peer, byte[]? avatarPng)
    {
        var window = new PeerProfileWindow();
        window.Load(peer, avatarPng);
        return window.ShowDialog(owner);
    }

    private void Load(PeerInfo peer, byte[]? avatarPng)
    {
        NameText.Text = peer.DisplayName;
        JobTitleText.Text = string.IsNullOrWhiteSpace(peer.JobTitle) ? "없음" : peer.JobTitle;
        GroupText.Text = string.IsNullOrWhiteSpace(peer.Group) ? "없음" : peer.Group;
        EmailText.Text = string.IsNullOrWhiteSpace(peer.Email) ? "없음" : peer.Email;
        MemoText.Text = string.IsNullOrWhiteSpace(peer.Memo) ? "없음" : peer.Memo;
        StatusText.Text = peer.IsAway ? "부재중" : "온라인";
        StatusText.Foreground = new SolidColorBrush(Color.Parse(peer.IsAway ? "#D97706" : "#059669"));

        if (avatarPng is not { Length: > 0 })
        {
            return;
        }

        _avatar = new Bitmap(new MemoryStream(avatarPng));
        AvatarImage.Source = _avatar;
        AvatarImage.IsVisible = true;
        AvatarPlaceholder.IsVisible = false;
        Closed += (_, _) => _avatar?.Dispose();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
