using System.Collections.ObjectModel;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.ViewModels;

public sealed class PeerNodeItem : ViewModelBase
{
    public PeerInfo Peer { get; }
    public string DisplayName => Peer.DisplayName;
    public string IpAddress => Peer.IpAddress;
    public bool IsAway => Peer.IsAway;
    public string StatusText => Peer.IsAway ? "부재중" : "온라인";

    public PeerNodeItem(PeerInfo peer)
    {
        Peer = peer;
    }
}

public sealed class GroupNodeItem : ViewModelBase
{
    private string _groupName;
    private bool _isExpanded;

    public string GroupId { get; }
    public string GroupName
    {
        get => _groupName;
        set
        {
            if (SetProperty(ref _groupName, value))
            {
                OnPropertyChanged(nameof(HeaderText));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<PeerNodeItem> Members { get; } = new();

    public int OnlineCount => Members.Count(m => !m.IsAway);
    public string HeaderText => $"{GroupName} ({Members.Count}명)";

    public GroupNodeItem(string groupId, string groupName, bool isExpanded = true)
    {
        GroupId = groupId;
        _groupName = groupName;
        _isExpanded = isExpanded;
    }

    public void NotifyMembersChanged()
    {
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(HeaderText));
    }
}

public sealed class UserGroupTreeViewModel : ViewModelBase
{
    public ObservableCollection<GroupNodeItem> Groups { get; } = new();

    public void UpdatePeers(IEnumerable<PeerInfo> peers)
    {
        var peerList = peers.ToList();
        var grouped = peerList.GroupBy(p => string.IsNullOrWhiteSpace(p.Group) ? "기타/미지정" : p.Group.Trim())
                              .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

        Groups.Clear();

        foreach (var g in grouped)
        {
            var node = new GroupNodeItem(g.Key, g.Key, isExpanded: true);
            foreach (var peer in g.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                node.Members.Add(new PeerNodeItem(peer));
            }
            node.NotifyMembersChanged();
            Groups.Add(node);
        }
    }
}
