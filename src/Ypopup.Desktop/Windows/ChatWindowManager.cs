using Avalonia.Controls;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Windows;

public interface IChatWindowManager
{
    void OpenRoomWindow(string roomId, ChatRoom room);
    void CloseRoomWindow(string roomId);
    bool IsRoomOpen(string roomId);
    Window? GetRoomWindow(string roomId);
}

public sealed class ChatWindowManager : IChatWindowManager
{
    private readonly Dictionary<string, Window> _openWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<ChatRoom, Window> _windowFactory;

    public ChatWindowManager(Func<ChatRoom, Window> windowFactory)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
    }

    public void OpenRoomWindow(string roomId, ChatRoom room)
    {
        if (_openWindows.TryGetValue(roomId, out var existing))
        {
            existing.Show();
            existing.Activate();
            return;
        }

        var win = _windowFactory(room);
        _openWindows[roomId] = win;

        win.Closed += (_, _) =>
        {
            _openWindows.Remove(roomId);
        };

        win.Show();
        win.Activate();
    }

    public void CloseRoomWindow(string roomId)
    {
        if (_openWindows.TryGetValue(roomId, out var existing))
        {
            existing.Close();
            _openWindows.Remove(roomId);
        }
    }

    public bool IsRoomOpen(string roomId) => _openWindows.ContainsKey(roomId);

    public Window? GetRoomWindow(string roomId)
    {
        _openWindows.TryGetValue(roomId, out var win);
        return win;
    }
}
