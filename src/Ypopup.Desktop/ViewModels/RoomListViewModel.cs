using System.Collections.ObjectModel;
using Ypopup.Core.Contracts;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.ViewModels;

public sealed class RoomItemViewModel : ViewModelBase
{
    private string _title;
    private string _lastMessage;
    private DateTime _lastMessageTime;
    private int _unreadCount;

    public string RoomId { get; }
    public RoomType Type { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string LastMessage
    {
        get => _lastMessage;
        set => SetProperty(ref _lastMessage, value);
    }

    public DateTime LastMessageTime
    {
        get => _lastMessageTime;
        set => SetProperty(ref _lastMessageTime, value);
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set => SetProperty(ref _unreadCount, value);
    }

    public int ParticipantCount { get; }

    public RoomItemViewModel(ChatRoom room)
    {
        RoomId = room.RoomId;
        Type = room.Type;
        _title = room.Title;
        _lastMessage = string.Empty;
        _lastMessageTime = room.LastMessageAtUtc.ToLocalTime();
        ParticipantCount = room.ParticipantIds.Count;
    }
}

public sealed class RoomListViewModel : ViewModelBase
{
    private readonly IChatRoomManager _roomManager;
    private RoomItemViewModel? _selectedRoom;

    public ObservableCollection<RoomItemViewModel> Rooms { get; } = new();

    public RoomItemViewModel? SelectedRoom
    {
        get => _selectedRoom;
        set => SetProperty(ref _selectedRoom, value);
    }

    public RoomListViewModel(IChatRoomManager roomManager)
    {
        _roomManager = roomManager ?? throw new ArgumentNullException(nameof(roomManager));
        _roomManager.OnRoomCreatedOrUpdated += HandleRoomUpdated;
        _roomManager.OnRoomRemoved += HandleRoomRemoved;
    }

    public async Task LoadRoomsAsync()
    {
        var list = await _roomManager.GetActiveRoomsAsync();
        Rooms.Clear();
        foreach (var r in list)
        {
            Rooms.Add(new RoomItemViewModel(r));
        }
    }

    private void HandleRoomUpdated(ChatRoom room)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var existing = Rooms.FirstOrDefault(r => r.RoomId == room.RoomId);
            if (existing is not null)
            {
                existing.Title = room.Title;
                existing.LastMessageTime = room.LastMessageAtUtc.ToLocalTime();
            }
            else
            {
                Rooms.Insert(0, new RoomItemViewModel(room));
            }
        });
    }

    private void HandleRoomRemoved(string roomId)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var existing = Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (existing is not null)
            {
                Rooms.Remove(existing);
            }
        });
    }
}
