using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Ypopup.Core.Models;

namespace Ypopup.Core.Protocol;

public static class ExtendedPacketCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static byte[] Serialize(ExtendedLanPacket packet)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, JsonOptions));
    }

    public static ExtendedLanPacket Deserialize(byte[] payload)
    {
        return JsonSerializer.Deserialize<ExtendedLanPacket>(payload, JsonOptions)
               ?? throw new InvalidDataException("패킷을 해석할 수 없습니다.");
    }

    public static async Task WritePacketAsync(Stream stream, ExtendedLanPacket packet, CancellationToken cancellationToken)
    {
        var payload = Serialize(packet);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ExtendedLanPacket?> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        var read = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        if (length <= 0 || length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException($"잘못된 패킷 크기: {length}");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return Deserialize(payload);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0 ? 0 : throw new EndOfStreamException("스트림이 예기치 않게 종료되었습니다.");
            }

            offset += read;
        }

        return offset;
    }
}
