using System.Buffers.Binary;
using System.Text;
using Xunit;
using Ypopup.Core.Models;
using Ypopup.Core.Protocol;
using Ypopup.Core.Tests;

namespace Ypopup.Core.Tests.Protocol;

public class PacketCodecTests
{
    private static LanPacket NewPacket() => new()
    {
        Type = PacketType.TextMessage,
        SenderId = "s1",
        SenderName = "tester",
        Body = "hello"
    };

    [Fact]
    public void Serialize_Deserialize_RoundTrip_PreservesFields()
    {
        var packet = NewPacket();

        var bytes = PacketCodec.Serialize(packet);
        var restored = PacketCodec.Deserialize(bytes);

        Assert.Equal(packet.MessageId, restored.MessageId);
        Assert.Equal(packet.SenderId, restored.SenderId);
        Assert.Equal(packet.SenderName, restored.SenderName);
        Assert.Equal(packet.Body, restored.Body);
        Assert.Equal(packet.Type, restored.Type);
    }

    [Fact]
    public void Deserialize_InvalidJson_Throws()
    {
        var badBytes = Encoding.UTF8.GetBytes("{not-json}");

        Assert.Throws<System.Text.Json.JsonException>(() => PacketCodec.Deserialize(badBytes));
    }

    [Fact]
    public async Task WritePacket_ReadPacket_HeaderRoundTrip()
    {
        using var stream = new MemoryStream();
        var packet = NewPacket();

        await PacketCodec.WritePacketAsync(stream, packet, CancellationToken.None);
        stream.Position = 0;

        var restored = await PacketCodec.ReadPacketAsync(stream, CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(packet.MessageId, restored!.MessageId);
        Assert.Equal(packet.Body, restored.Body);
    }

    [Fact]
    public async Task ReadPacketAsync_TooLargePayload_ThrowsInvalidData()
    {
        var tooLarge = 16 * 1024 * 1024 + 1;
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, tooLarge);

        using var stream = new MemoryStream();
        await stream.WriteAsync(header, CancellationToken.None);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PacketCodec.ReadPacketAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task ReadPacketAsync_NonPositiveLength_ThrowsInvalidData()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, 0);

        using var stream = new MemoryStream();
        await stream.WriteAsync(header, CancellationToken.None);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PacketCodec.ReadPacketAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task ReadPacketAsync_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        var result = await PacketCodec.ReadPacketAsync(stream, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveFileAsync_TruncatedStream_ThrowsEndOfStream()
    {
        var expectedSize = 1024L;
        var buffer = new byte[256];
        using var source = new MemoryStream(buffer);

        using var tempDir = new TempDir();
        var dest = Path.Combine(tempDir.Path, "out.bin");

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PacketCodec.SaveFileAsync(source, dest, expectedSize, CancellationToken.None));
    }

    [Fact]
    public async Task SaveFileAsync_Cancelled_ThrowsOperationCanceled()
    {
        var expectedSize = 4096L;
        using var source = new CancellingStream();
        using var tempDir = new TempDir();
        var dest = Path.Combine(tempDir.Path, "out.bin");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => PacketCodec.SaveFileAsync(source, dest, expectedSize, cts.Token));
    }

    private sealed class CancellingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}