using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.ViewModels;

public sealed class PeerNodeItem : ViewModelBase
{
    private bool _isChecked;
    private bool _isSelected;
    private Bitmap? _avatar;

    public PeerInfo Peer { get; }
    public string DisplayName => Peer.DisplayName;
    public string IpAddress => Peer.IpAddress;
    public bool IsAway => Peer.IsAway;
    public Bitmap? Avatar => _avatar;
    public bool HasAvatar => _avatar is not null;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public PeerNodeItem(PeerInfo peer, byte[]? avatarPng = null)
    {
        Peer = peer;
        if (avatarPng is { Length: > 0 })
        {
            SetAvatar(avatarPng);
        }
    }

    public void SetAvatar(byte[] png)
    {
        var next = new Bitmap(new MemoryStream(png));
        var previous = _avatar;
        _avatar = next;
        OnPropertyChanged(nameof(Avatar));
        OnPropertyChanged(nameof(HasAvatar));
        previous?.Dispose();
    }

    public void ReleaseAvatar()
    {
        _avatar?.Dispose();
        _avatar = null;
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

    public void UpdatePeers(IEnumerable<PeerInfo> peers, Func<PeerInfo, byte[]?>? avatarLookup = null)
    {
        ReleaseAvatars();
        var peerList = peers.ToList();
        var grouped = peerList.GroupBy(p => string.IsNullOrWhiteSpace(p.Group) ? "기타/미지정" : p.Group.Trim())
                              .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

        Groups.Clear();

        foreach (var g in grouped)
        {
            var node = new GroupNodeItem(g.Key, g.Key, isExpanded: true);
            foreach (var peer in g.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                node.Members.Add(new PeerNodeItem(peer, avatarLookup?.Invoke(peer)));
            }
            node.NotifyMembersChanged();
            Groups.Add(node);
        }
    }

    public void ReleaseAvatars()
    {
        foreach (var member in Groups.SelectMany(group => group.Members))
        {
            member.ReleaseAvatar();
        }
    }

    public PeerInfo? SelectedPeer =>
        Groups.SelectMany(group => group.Members).FirstOrDefault(member => member.IsSelected)?.Peer;

    public void SelectMachine(string? machineId)
    {
        foreach (var member in Groups.SelectMany(group => group.Members))
        {
            member.IsSelected = machineId is not null
                && string.Equals(member.Peer.MachineId, machineId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void ApplyAvatar(string machineId, string avatarHash, byte[] png)
    {
        foreach (var member in Groups.SelectMany(group => group.Members))
        {
            if (string.Equals(member.Peer.MachineId, machineId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(member.Peer.AvatarHash, avatarHash, StringComparison.Ordinal))
            {
                member.SetAvatar(png);
            }
        }
    }
}
