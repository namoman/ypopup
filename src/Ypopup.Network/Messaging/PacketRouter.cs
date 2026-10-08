using System.Collections.Concurrent;
using Ypopup.Core.Contracts;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;

namespace Ypopup.Network.Messaging;

public interface IPacketRouter
{
    void RegisterRoomHandler(string roomId, Action<ExtendedLanPacket> handler);
    void UnregisterRoomHandler(string roomId);
    void RouteIncomingPacket(ExtendedLanPacket packet);
    event Action<ExtendedLanPacket>? OnUnregisteredRoomPacketReceived;
    event Action<ExtendedLanPacket>? OnDirectMessageReceived;
}

public sealed class PacketRouter : IPacketRouter
{
    private readonly ConcurrentDictionary<string, Action<ExtendedLanPacket>> _roomHandlers = new(StringComparer.OrdinalIgnoreCase);

    public event Action<ExtendedLanPacket>? OnUnregisteredRoomPacketReceived;
    public event Action<ExtendedLanPacket>? OnDirectMessageReceived;

    public void RegisterRoomHandler(string roomId, Action<ExtendedLanPacket> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(handler);
        _roomHandlers[roomId] = handler;
    }

    public void UnregisterRoomHandler(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        _roomHandlers.TryRemove(roomId, out _);
    }

    public void RouteIncomingPacket(ExtendedLanPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (string.IsNullOrWhiteSpace(packet.RoomId))
        {
            try
            {
                OnDirectMessageReceived?.Invoke(packet);
            }
            catch (Exception ex)
            {
                LogService.Error("PacketRouter", $"Direct message handle error: {ex.Message}");
            }
            return;
        }

        if (_roomHandlers.TryGetValue(packet.RoomId, out var handler))
        {
            try
            {
                handler.Invoke(packet);
            }
            catch (Exception ex)
            {
                LogService.Error("PacketRouter", $"Room {packet.RoomId} handler error: {ex.Message}");
            }
        }
        else
        {
            try
            {
                OnUnregisteredRoomPacketReceived?.Invoke(packet);
            }
            catch (Exception ex)
            {
                LogService.Error("PacketRouter", $"Unregistered room message handle error: {ex.Message}");
            }
        }
    }
}
