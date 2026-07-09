using Xunit;
using Ypopup.Core.Models;
using Ypopup.Core.Protocol;
using Ypopup.Core.Tests;

namespace Ypopup.Core.Tests.Transfers;

public class FileReceiveProgressTests
{
    [Fact]
    public async Task SaveFileAsync_ReportsProgressForLargeFile()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "received.bin");

        const long size = 4 * 1024 * 1024;
        var content = new byte[size];
        using var source = new MemoryStream(content);

        var received = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(received.Add);

        await PacketCodec.SaveFileAsync(source, dest, size, CancellationToken.None, progress);

        Assert.NotEmpty(received);
        Assert.All(received, p => Assert.False(p.IsSending));
        Assert.True(received[^1].IsComplete);
        Assert.Equal(size, received[^1].TotalBytes);
    }

    [Fact]
    public async Task SaveFileAsync_CancellationDuringRead_ThrowsOperationCanceled()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "received.bin");

        var source = new CancellingReadStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => PacketCodec.SaveFileAsync(source, dest, 4096L, cts.Token, progress: null));
    }

    [Fact]
    public async Task SaveFileAsync_SmallFile_ReportsStartAndEnd()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "tiny.bin");

        var content = new byte[] { 1, 2, 3 };
        using var source = new MemoryStream(content);

        var received = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(received.Add);

        await PacketCodec.SaveFileAsync(source, dest, content.Length, CancellationToken.None, progress);

        Assert.NotEmpty(received);
        Assert.True(received[^1].IsComplete);
    }

    private sealed class CancellingReadStream : Stream
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