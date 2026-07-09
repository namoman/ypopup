using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Ypopup.Core.Models;

namespace Ypopup.Core.Protocol;

public static class PacketCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static byte[] Serialize(LanPacket packet)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, JsonOptions));
    }

    public static LanPacket Deserialize(byte[] payload)
    {
        return JsonSerializer.Deserialize<LanPacket>(payload, JsonOptions)
               ?? throw new InvalidDataException("패킷을 해석할 수 없습니다.");
    }

    public static async Task WritePacketAsync(Stream stream, LanPacket packet, CancellationToken cancellationToken)
    {
        var payload = Serialize(packet);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<LanPacket?> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
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

    public static Task WriteFileAsync(Stream stream, string filePath, CancellationToken cancellationToken)
        => WriteFileAsync(stream, filePath, cancellationToken, progress: null);

    public static async Task WriteFileAsync(
        Stream stream,
        string filePath,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var reporter = new ProgressThresholdReporter(fileStream.Length);
        reporter.ReportIfReady(progress, 0, isSending: true, Path.GetFileName(filePath));

        var buffer = new byte[81920];
        long transferred = 0;
        int bytesRead;

        cancellationToken.ThrowIfCancellationRequested();
        while ((bytesRead = await fileStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            transferred += bytesRead;
            reporter.ReportIfReady(progress, transferred, isSending: true, Path.GetFileName(filePath));
        }

        reporter.ReportIfReady(progress, transferred, isSending: true, Path.GetFileName(filePath));
    }

    public static Task SaveFileAsync(
        Stream stream,
        string destinationPath,
        long expectedSize,
        CancellationToken cancellationToken)
        => SaveFileAsync(stream, destinationPath, expectedSize, cancellationToken, progress: null);

    public static async Task SaveFileAsync(
        Stream stream,
        string destinationPath,
        long expectedSize,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var reporter = new ProgressThresholdReporter(expectedSize);
        reporter.ReportIfReady(progress, 0, isSending: false, Path.GetFileName(destinationPath));

        var buffer = new byte[81920];
        long remaining = expectedSize;
        long transferred = 0;

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("파일 수신이 중간에 끊겼습니다.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            remaining -= read;
            transferred += read;
            reporter.ReportIfReady(progress, transferred, isSending: false, Path.GetFileName(destinationPath));
        }
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
