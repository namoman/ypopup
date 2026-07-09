using System.Net.Sockets;
using Xunit;
using Ypopup.Core.Models;
using Ypopup.Core.Settings;
using Ypopup.Core.Sharing;
using Ypopup.Network.Sharing;

namespace Ypopup.Network.Tests.Sharing;

[Collection("SharedFolderIntegration")]
public class SharedFolderIntegrationTests : IAsyncLifetime
{
    private readonly TempDir _tempDir = new();
    private readonly SettingsService _settingsService;
    private readonly SharedFolderHostService _hostService;
    private readonly PeerInfo _self;
    private readonly int _port;

    public SharedFolderIntegrationTests()
    {
        _port = FindFreePort();
        _settingsService = new SettingsService(_tempDir.Path);
        _settingsService.Save(new AppSettings
        {
            ShareFolderEnabled = true,
            ShareFolderPath = _tempDir.Path,
            ShareFolderPort = _port
        });

        _self = new PeerInfo
        {
            MachineId = "test-self",
            DisplayName = "tester",
            IpAddress = "127.0.0.1",
            ShareFolderPort = _port
        };

        _hostService = new SharedFolderHostService(_settingsService);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDir.Path);

        await File.WriteAllTextAsync(Path.Combine(_tempDir.Path, "a.txt"), "AAA");
        await File.WriteAllTextAsync(Path.Combine(_tempDir.Path, "b.txt"), "BBB");
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir.Path, "sub"));
        await File.WriteAllTextAsync(Path.Combine(sub.FullName, "c.txt"), "CCC");
        var empty = Directory.CreateDirectory(Path.Combine(_tempDir.Path, "empty"));

        var result = await _hostService.StartAsync(CancellationToken.None);
        Assert.True(result.IsRunning, $"서버 시작 실패: {result.ErrorMessage}");
    }

    public async Task DisposeAsync()
    {
        await _hostService.StopAsync();
        _tempDir.Dispose();
    }

    [Fact]
    public async Task List_Root_ReturnsAllEntries()
    {
        var response = await SharedFolderClient.ListAsync(_self, string.Empty);

        Assert.Equal("", response.CurrentPath);
        Assert.Contains(response.Entries, e => e.Name == "a.txt" && !e.IsDirectory);
        Assert.Contains(response.Entries, e => e.Name == "b.txt" && !e.IsDirectory);
        Assert.Contains(response.Entries, e => e.Name == "sub" && e.IsDirectory);
        Assert.Contains(response.Entries, e => e.Name == "empty" && e.IsDirectory);
    }

    [Fact]
    public async Task List_Subdirectory_ReturnsItsEntries()
    {
        var response = await SharedFolderClient.ListAsync(_self, "sub");

        Assert.Contains(response.Entries, e => e.Name == "c.txt" && !e.IsDirectory);
    }

    [Fact]
    public async Task List_EmptyDirectory_ReturnsEmpty()
    {
        var response = await SharedFolderClient.ListAsync(_self, "empty");

        Assert.Empty(response.Entries);
    }

    [Fact]
    public async Task List_NonExistentPath_Throws()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => SharedFolderClient.ListAsync(_self, "nonexistent"));

        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task Download_ExistingFile_ReturnsContent()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "downloaded.txt");

        await SharedFolderClient.DownloadAsync(_self, "a.txt", dest);

        Assert.True(File.Exists(dest));
        var content = await File.ReadAllTextAsync(dest);
        Assert.Equal("AAA", content);
    }

    [Fact]
    public async Task Download_NonExistentFile_Throws()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "missing.bin");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => SharedFolderClient.DownloadAsync(_self, "missing.txt", dest));

        Assert.False(File.Exists(dest), "대상 파일이 생성되지 않아야 합니다.");
    }

    [Fact]
    public async Task Download_PathTraversal_Throws()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "escaped.bin");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => SharedFolderClient.DownloadAsync(_self, "../../etc/passwd", dest));
    }

    [Fact]
    public async Task List_PathTraversal_Throws()
    {
        await Assert.ThrowsAsync<HttpRequestException>(
            () => SharedFolderClient.ListAsync(_self, "../outside"));
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ypopup-net-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}