using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ypopup.Core.Models;
using Ypopup.Desktop.ViewModels;

namespace Ypopup.Desktop.Views.UserList;

public partial class UserGroupTreeControl : UserControl
{
    public event Action<PeerInfo>? PeerSelected;
    public event Action<PeerInfo>? PeerDoubleTapped;
    public event Action<PeerInfo>? ShareFolderRequested;

    public UserGroupTreeControl()
    {
        InitializeComponent();
    }

    private void OnMemberSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: PeerNodeItem node })
        {
            PeerSelected?.Invoke(node.Peer);
        }
    }

    private void OnMemberDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: PeerNodeItem node })
        {
            PeerDoubleTapped?.Invoke(node.Peer);
        }
    }

    private void ShareFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PeerNodeItem node })
        {
            ShareFolderRequested?.Invoke(node.Peer);
        }
    }
}
