using Xunit;
using Ypopup.Core.Models;
using Ypopup.Core.Protocol;
using Ypopup.Core.Tests;

namespace Ypopup.Core.Tests.Transfers;

public class FileSendProgressTests
{
    [Fact]
    public async Task WriteFileAsync_ReportsProgressForLargeFile()
    {
        using var temp = new TempDir();
        var filePath = Path.Combine(temp.Path, "payload.bin");

        const long size = 4 * 1024 * 1024;
        var content = new byte[size];
        await File.WriteAllBytesAsync(filePath, content);

        var received = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(received.Add);

        using var output = new MemoryStream();
        await PacketCodec.WriteFileAsync(output, filePath, CancellationToken.None, progress);

        Assert.NotEmpty(received);
        Assert.All(received, p => Assert.True(p.IsSending));
        var final = received[^1];
        Assert.True(final.BytesTransferred >= size);
        Assert.Equal(size, final.TotalBytes);
        Assert.True(final.IsComplete);
    }

    [Fact]
    public async Task WriteFileAsync_Cancelled_ThrowsAndNoProgress()
    {
        using var temp = new TempDir();
        var filePath = Path.Combine(temp.Path, "big.bin");

        const long size = 16 * 1024 * 1024;
        var content = new byte[size];
        await File.WriteAllBytesAsync(filePath, content);

        var received = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(received.Add);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        using var output = new BlockingStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PacketCodec.WriteFileAsync(output, filePath, cts.Token, progress));
    }

    [Fact]
    public async Task WriteFileAsync_SmallFile_ReportsAtLeastStartAndEnd()
    {
        using var temp = new TempDir();
        var filePath = Path.Combine(temp.Path, "tiny.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        var received = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(received.Add);

        using var output = new MemoryStream();
        await PacketCodec.WriteFileAsync(output, filePath, CancellationToken.None, progress);

        Assert.NotEmpty(received);
        Assert.True(received[^1].IsComplete);
        Assert.Equal(5L, received[^1].TotalBytes);
    }

    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}