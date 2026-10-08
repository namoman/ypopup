using Avalonia.Controls;
using Avalonia.Input;
using Ypopup.Core.Models;
using Ypopup.Desktop.ViewModels;

namespace Ypopup.Desktop.Views.UserList;

public partial class UserGroupTreeControl : UserControl
{
    public event Action<PeerInfo>? PeerDoubleTapped;

    public UserGroupTreeControl()
    {
        InitializeComponent();
    }

    private void OnMemberDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is PeerNodeItem node)
        {
            PeerDoubleTapped?.Invoke(node.Peer);
        }
    }
}
