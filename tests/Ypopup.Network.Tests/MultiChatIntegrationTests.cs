using Xunit;
using Ypopup.Core.Contracts;
using Ypopup.Core.Models;
using Ypopup.Core.Protocol;
using Ypopup.Core.Storage;
using Ypopup.Network.Messaging;

namespace Ypopup.Network.Tests;

public class MultiChatIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly JsonChatStorageRepository _storage;

    public MultiChatIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "YpopupTests_" + Guid.NewGuid().ToString("N"));
        _storage = new JsonChatStorageRepository(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public void PacketRouter_RoutesToRegisteredRoomHandler()
    {
        // Arrange
        var router = new PacketRouter();
        var targetRoomId = "room-123";
        ExtendedLanPacket? receivedPacket = null;

        router.RegisterRoomHandler(targetRoomId, packet =>
        {
            receivedPacket = packet;
        });

        var testPacket = new ExtendedLanPacket
        {
            RoomId = targetRoomId,
            SenderId = "user-1",
            Body = "안녕하세요!"
        };

        // Act
        router.RouteIncomingPacket(testPacket);

        // Assert
        Assert.NotNull(receivedPacket);
        Assert.Equal(targetRoomId, receivedPacket.RoomId);
        Assert.Equal("안녕하세요!", receivedPacket.Body);
    }

    [Fact]
    public void PacketRouter_TriggersUnregisteredEvent_WhenRoomNotRegistered()
    {
        // Arrange
        var router = new PacketRouter();
        ExtendedLanPacket? unregPacket = null;

        router.OnUnregisteredRoomPacketReceived += packet =>
        {
            unregPacket = packet;
        };

        var testPacket = new ExtendedLanPacket
        {
            RoomId = "unknown-room",
            SenderId = "user-2",
            Body = "새 방 메시지"
        };

        // Act
        router.RouteIncomingPacket(testPacket);

        // Assert
        Assert.NotNull(unregPacket);
        Assert.Equal("unknown-room", unregPacket.RoomId);
    }

    [Fact]
    public async Task JsonChatStorageRepository_SavesAndRetrievesRoomsAndMessages()
    {
        // Arrange
        var roomId = "room-abc";
        var room = new ChatRoom(
            RoomId: roomId,
            Title: "테스트 간호부 단체방",
            Type: RoomType.GroupChat,
            ParticipantIds: new[] { "user-1", "user-2", "user-3" }
        );

        var msg = new ChatMessage(
            MessageId: "msg-001",
            RoomId: roomId,
            SenderId: "user-1",
            SenderName: "수간호사",
            Content: "오늘 회의 일정 공유합니다.",
            Type: MessageType.Text,
            TimestampUtc: DateTime.UtcNow
        );

        // Act
        await _storage.SaveRoomAsync(room);
        await _storage.AppendMessageAsync(msg);

        var loadedRoom = await _storage.LoadRoomAsync(roomId);
        var recentMsgs = await _storage.GetRecentMessagesAsync(roomId);

        // Assert
        Assert.NotNull(loadedRoom);
        Assert.Equal("테스트 간호부 단체방", loadedRoom.Title);
        Assert.Equal(3, loadedRoom.ParticipantIds.Count);

        Assert.Single(recentMsgs);
        Assert.Equal("오늘 회의 일정 공유합니다.", recentMsgs[0].Content);
        Assert.Equal("수간호사", recentMsgs[0].SenderName);
    }

    [Fact]
    public void ExtendedPacketCodec_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var packet = new ExtendedLanPacket
        {
            Type = ExtendedPacketType.GroupChatMessage,
            MessageId = "packet-id-999",
            SenderId = "sender-peer",
            SenderName = "원무과장",
            Body = "연휴 근무 안내",
            RoomId = "room-notice-1",
            ParticipantIds = new List<string> { "peer-a", "peer-b", "peer-c" }
        };

        // Act
        var bytes = ExtendedPacketCodec.Serialize(packet);
        var deserialized = ExtendedPacketCodec.Deserialize(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(packet.MessageId, deserialized.MessageId);
        Assert.Equal(packet.RoomId, deserialized.RoomId);
        Assert.Equal(ExtendedPacketType.GroupChatMessage, deserialized.Type);
        Assert.Equal(3, deserialized.ParticipantIds.Count);
        Assert.Contains("peer-b", deserialized.ParticipantIds);
    }
}
