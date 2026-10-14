using System.Net.Sockets;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Protocol;

namespace Ypopup.Network.Messaging;

public sealed record SendResult(string PeerId, bool IsSuccess, string? ErrorMessage = null);

public interface IMessageDispatcher
{
    Task<IReadOnlyList<SendResult>> DispatchRoomMessageAsync(
        string roomId,
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default);

    Task<IReadOnlyList<SendResult>> MultiCastNoticeAsync(
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default);
}

public sealed class MessageDispatcher : IMessageDispatcher
{
    private const int ConnectionTimeoutMs = 3000;

    public async Task<IReadOnlyList<SendResult>> DispatchRoomMessageAsync(
        string roomId,
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default)
    {
        packet.RoomId = roomId;
        packet.Type = ExtendedPacketType.GroupChatMessage;
        return await SendToPeersParallelAsync(packet, targetPeers, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SendResult>> MultiCastNoticeAsync(
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default)
    {
        packet.Type = ExtendedPacketType.MultiCastDirectNotice;
        return await SendToPeersParallelAsync(packet, targetPeers, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<SendResult>> SendToPeersParallelAsync(
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct)
    {
        var peerList = targetPeers.ToList();
        var tasks = peerList.Select(peer => SendSinglePeerAsync(peer, packet, ct));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    private static async Task<SendResult> SendSinglePeerAsync(PeerUser peer, ExtendedLanPacket packet, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(ConnectionTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await client.ConnectAsync(peer.IpAddress, peer.TcpPort, linkedCts.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            await ExtendedPacketCodec.WritePacketAsync(stream, packet, linkedCts.Token).ConfigureAwait(false);
            return new SendResult(peer.UserId, true);
        }
        catch (Exception ex)
        {
            LogService.Warning("MessageDispatcher", $"Send failed to {peer.DisplayName} ({peer.IpAddress}:{peer.TcpPort}): {ex.Message}");
            return new SendResult(peer.UserId, false, ex.Message);
        }
    }
}
