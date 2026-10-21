using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Settings;
using Ypopup.Desktop.Helpers;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class ProfileSettingsPanel : UserControl
{
    private byte[]? _pendingAvatar;
    private bool _avatarChanged;
    private Bitmap? _preview;

    public ProfileSettingsPanel()
    {
        InitializeComponent();
    }

    public void Load(AppSettings settings)
    {
        DisplayNameTextBox.Text = settings.DisplayName;
        JobTitleTextBox.Text = settings.JobTitle;
        GroupTextBox.Text = settings.Group;
        OnlySameGroupCheckBox.IsChecked = settings.OnlySameGroup;
        EmailTextBox.Text = settings.Email;
        MemoTextBox.Text = settings.Memo;
        _avatarChanged = false;
        _pendingAvatar = null;
        ShowPreview(AvatarStore.ReadBytes());
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.DisplayName = DisplayNameTextBox.Text?.Trim() ?? string.Empty;
        settings.JobTitle = JobTitleTextBox.Text?.Trim() ?? string.Empty;
        settings.Group = GroupTextBox.Text?.Trim() ?? string.Empty;
        settings.OnlySameGroup = OnlySameGroupCheckBox.IsChecked == true;
        settings.Email = EmailTextBox.Text?.Trim() ?? string.Empty;
        settings.Memo = MemoTextBox.Text?.Trim() ?? string.Empty;
    }

    public void CommitAvatar()
    {
        if (!_avatarChanged)
        {
            return;
        }

        if (_pendingAvatar is null)
        {
            AvatarStore.Delete();
            return;
        }

        AvatarStore.Save(_pendingAvatar);
    }

    private async void PickAvatar_Click(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "프로필 사진",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("그림")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"]
                }
            ]
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null)
        {
            return;
        }

        try
        {
            var path = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("선택한 파일 경로를 열 수 없습니다.");
            }

            var png = AvatarImage.CreateThumbnailPng(path);
            _pendingAvatar = png;
            _avatarChanged = true;
            ShowPreview(png);
        }
        catch (Exception ex)
        {
            LogService.Warning("Profile", $"프로필 사진을 읽지 못했습니다: {ex.Message}");
            var owner = TopLevel.GetTopLevel(this) as Window;
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", $"그림을 열 수 없습니다.\n\n{ex.Message}");
        }
    }

    private void ClearAvatar_Click(object? sender, RoutedEventArgs e)
    {
        _pendingAvatar = null;
        _avatarChanged = true;
        ShowPreview(null);
    }

    private void ShowPreview(byte[]? png)
    {
        Bitmap? next = null;
        if (png is { Length: > 0 })
        {
            next = new Bitmap(new MemoryStream(png));
        }

        AvatarPreview.Source = next;
        AvatarPreview.IsVisible = next is not null;
        AvatarPlaceholder.IsVisible = next is null;
        ClearAvatarButton.IsVisible = next is not null;
        var previous = _preview;
        _preview = next;
        previous?.Dispose();
    }
}
