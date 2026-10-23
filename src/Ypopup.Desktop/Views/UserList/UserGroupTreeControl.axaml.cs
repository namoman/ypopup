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
    public event Action<PeerInfo>? AvatarRequested;

    public UserGroupTreeControl()
    {
        InitializeComponent();
    }

    private void OnMemberSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: PeerNodeItem node })
        {
            return;
        }

        if (DataContext is UserGroupTreeViewModel viewModel)
        {
            viewModel.SelectMachine(node.Peer.MachineId);
        }

        PeerSelected?.Invoke(node.Peer);
    }

    private void OnMemberDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: PeerNodeItem node })
        {
            PeerDoubleTapped?.Invoke(node.Peer);
        }
    }

    private void AvatarButton_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: PeerNodeItem node })
        {
            AvatarRequested?.Invoke(node.Peer);
        }
    }

    private void AvatarButton_DoubleTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
    }

    private void ShareFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PeerNodeItem node })
        {
            ShareFolderRequested?.Invoke(node.Peer);
        }
    }
}
