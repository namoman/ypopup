using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ypopup.Core.Models;
using Ypopup.Desktop.Helpers;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.SharedFolder;

public partial class SharedFolderWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly PeerInfo _peer;
    private string _currentPath = string.Empty;
    private bool _isLoading;

    public SharedFolderWindow(YpopupCoordinator coordinator, PeerInfo peer)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _peer = peer;
        Topmost = _coordinator.Settings.KeepWindowTopmost;
        TitleTextBlock.Text = $"{peer.DisplayName}의 공유폴더";
        Loaded += async (_, _) => await LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var response = await _coordinator.ListSharedFolderAsync(_peer, _currentPath);
            _currentPath = response.CurrentPath;
            PathTextBlock.Text = string.IsNullOrEmpty(_currentPath) ? "/" : $"/{_currentPath}";

            var entries = new List<SharedFolderEntry>();
            if (!string.IsNullOrEmpty(_currentPath))
            {
                entries.Add(new SharedFolderEntry
                {
                    Name = "..",
                    RelativePath = GetParentPath(_currentPath),
                    IsDirectory = true,
                    Size = 0
                });
            }

            entries.AddRange(response.Entries);
            EntryListBox.ItemsSource = entries;
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowWarningAsync(
                this,
                "Y-popup",
                $"공유폴더에 연결할 수 없습니다.\n\n{ex.Message}\n\n" +
                "상대 PC에서 공유폴더가 켜져 있는지, 방화벽 TCP 포트가 허용됐는지 확인하세요.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string GetParentPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }

    private async void UpButton_Click(object? sender, RoutedEventArgs e)
    {
        _currentPath = GetParentPath(_currentPath);
        await LoadEntriesAsync();
    }

    private async void EntryListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (EntryListBox.SelectedItem is not SharedFolderEntry entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            _currentPath = entry.RelativePath;
            await LoadEntriesAsync();
        }
    }

    private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (EntryListBox.SelectedItem is not SharedFolderEntry entry || entry.IsDirectory || entry.Name == "..")
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "다운로드할 파일을 선택하세요.");
            return;
        }

        var receiveDirectory = _coordinator.Settings.ReceiveDirectory;
        Directory.CreateDirectory(receiveDirectory);

        var startFolder = await StorageProvider.TryGetFolderFromPathAsync(receiveDirectory);
        var savedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "파일 저장",
            SuggestedFileName = entry.Name,
            SuggestedStartLocation = startFolder
        });

        var savePath = savedFile?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return;
        }

        try
        {
            await _coordinator.DownloadSharedFileAsync(_peer, entry.RelativePath, savePath);
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "다운로드가 완료되었습니다.");
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(this, "Y-popup", $"다운로드에 실패했습니다.\n\n{ex.Message}");
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
