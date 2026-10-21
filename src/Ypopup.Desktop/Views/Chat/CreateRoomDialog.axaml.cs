using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core.Models;
using Ypopup.Desktop.ViewModels;

namespace Ypopup.Desktop.Views.Chat;

public sealed class SelectablePeerItem : ViewModelBase
{
    private bool _isSelected;

    public PeerInfo Peer { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayText => $"[{(string.IsNullOrWhiteSpace(Peer.Group) ? "미지정" : Peer.Group)}] {Peer.DisplayName} ({Peer.IpAddress})";

    public SelectablePeerItem(PeerInfo peer)
    {
        Peer = peer;
    }
}

public sealed record CreateRoomDialogResult(
    string Title,
    string Body,
    IReadOnlyList<PeerInfo> SelectedPeers);

public partial class CreateRoomDialog : Window
{
    private readonly List<SelectablePeerItem> _allPeers;
    private readonly bool _pickOnly;
    public CreateRoomDialogResult? Result { get; private set; }

    public CreateRoomDialog()
    {
        InitializeComponent();
        _allPeers = [];
    }

    public CreateRoomDialog(IEnumerable<PeerInfo> peers) : this(peers, pickOnly: false)
    {
    }

    public CreateRoomDialog(IEnumerable<PeerInfo> peers, bool pickOnly) : this()
    {
        _pickOnly = pickOnly;
        _allPeers = peers.Select(p => new SelectablePeerItem(p)).ToList();
        if (_pickOnly)
        {
            Title = "받는 사람 추가";
            TitlePanel.IsVisible = false;
            BodyPanel.IsVisible = false;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? _allPeers
            : _allPeers.Where(p =>
                p.Peer.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Peer.Group.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        PeersListBox.ItemsSource = filtered;
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void SelectAllButton_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _allPeers)
        {
            item.IsSelected = true;
        }

        ApplyFilter();
    }

    private void DeselectAllButton_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _allPeers)
        {
            item.IsSelected = false;
        }

        ApplyFilter();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _allPeers.Where(p => p.IsSelected).Select(p => p.Peer).ToList();
        var title = RoomTitleTextBox.Text?.Trim() ?? string.Empty;
        var body = MessageBodyTextBox.Text?.Trim() ?? string.Empty;

        Result = new CreateRoomDialogResult(title, body, selected);
        Close();
    }
}
