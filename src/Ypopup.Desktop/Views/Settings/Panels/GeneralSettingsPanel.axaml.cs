using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Ypopup.Core.Models;
using Ypopup.Core.Sharing;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Platform.Startup;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class GeneralSettingsPanel : UserControl
{
    private AppSettings _workingSettings = new();
    private IStartupService _startupService = StartupServiceFactory.Create();

    public GeneralSettingsPanel()
    {
        InitializeComponent();
    }

    public void Initialize(IStartupService startupService)
    {
        _startupService = startupService;
    }

    public void Load(AppSettings settings)
    {
        _workingSettings = settings;
        RunAtStartupCheckBox.IsVisible = OperatingSystem.IsWindows();
        KeepWindowTopmostCheckBox.IsChecked = settings.KeepWindowTopmost;
        RunAtStartupCheckBox.IsChecked = _startupService.IsEnabled();
        CloseComposeAfterSendCheckBox.IsChecked = settings.CloseComposeWindowAfterSend;
        CloseReceiveOnReplyCheckBox.IsChecked = settings.CloseReceiveWindowOnReply;
        SoundEnabledCheckBox.IsChecked = settings.SoundEnabled;
        PlayMessageSoundCheckBox.IsChecked = settings.PlayMessageReceivedSound;
        PlayFileSoundCheckBox.IsChecked = settings.PlayFileReceivedSound;
        ReceiveDirectoryTextBox.Text = settings.ReceiveDirectory;
        ShareFolderEnabledCheckBox.IsChecked = settings.ShareFolderEnabled;
        ShareFolderPathTextBox.Text = settings.ShareFolderPath;
        UpdateShareFolderStatus(settings.ShareFolderPath);
        MessageFontHelper.ApplyPreview(settings, FontPreviewTextBlock);
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.KeepWindowTopmost = KeepWindowTopmostCheckBox.IsChecked == true;
        settings.CloseComposeWindowAfterSend = CloseComposeAfterSendCheckBox.IsChecked == true;
        settings.CloseReceiveWindowOnReply = CloseReceiveOnReplyCheckBox.IsChecked == true;
        settings.SoundEnabled = SoundEnabledCheckBox.IsChecked == true;
        settings.PlayMessageReceivedSound = PlayMessageSoundCheckBox.IsChecked == true;
        settings.PlayFileReceivedSound = PlayFileSoundCheckBox.IsChecked == true;
        settings.ReceiveDirectory = ReceiveDirectoryTextBox.Text?.Trim() ?? string.Empty;
        settings.ShareFolderEnabled = ShareFolderEnabledCheckBox.IsChecked == true;
        settings.ShareFolderPath = ShareFolderPathTextBox.Text?.Trim() ?? string.Empty;
        settings.MessageFontFamily = _workingSettings.MessageFontFamily;
        settings.MessageFontSize = _workingSettings.MessageFontSize;
    }

    public bool RunAtStartupEnabled => RunAtStartupCheckBox.IsChecked == true;
    public bool ShareFolderEnabled => ShareFolderEnabledCheckBox.IsChecked == true;
    public string ShareFolderPath => ShareFolderPathTextBox.Text?.Trim() ?? string.Empty;

    private void UpdateShareFolderStatus(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            ShareFolderStatusTextBlock.Text = "공유 경로가 비어 있습니다.";
            return;
        }

        if (!Directory.Exists(path))
        {
            ShareFolderStatusTextBlock.Text = "폴더가 없습니다. 저장 후 자동 생성되거나 경로를 확인하세요.";
            return;
        }

        var fileCount = Directory.GetFiles(path).Length;
        var folderCount = Directory.GetDirectories(path).Length;
        ShareFolderStatusTextBlock.Text =
            $"LAN에 공개되는 폴더: {path}\n파일 {fileCount}개, 하위 폴더 {folderCount}개";
    }

    private IStorageProvider? GetStorageProvider()
        => TopLevel.GetTopLevel(this)?.StorageProvider;

    private Window? GetOwnerWindow() => TopLevel.GetTopLevel(this) as Window;

    private async void ChangeFontButton_Click(object? sender, RoutedEventArgs e)
    {
        var owner = GetOwnerWindow();
        var fonts = FontManager.Current.SystemFonts.OrderBy(f => f.Name).ToList();
        var comboBox = new ComboBox
        {
            ItemsSource = fonts.Select(f => f.Name).ToList(),
            SelectedItem = _workingSettings.MessageFontFamily,
            Width = 280
        };

        var sizeBox = new NumericUpDown
        {
            Minimum = 8,
            Maximum = 72,
            Value = (decimal)_workingSettings.MessageFontSize,
            Width = 120,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock { Text = "글꼴" },
                comboBox,
                new TextBlock { Text = "크기", Margin = new Thickness(0, 8, 0, 0) },
                sizeBox
            }
        };

        var dialog = new Window
        {
            Title = "글꼴 선택",
            Width = 320,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Children =
                {
                    panel,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Margin = new Thickness(16, 0, 16, 16),
                        Children =
                        {
                            new Button
                            {
                                Content = "취소",
                                Classes = { "secondary" },
                                Margin = new Thickness(0, 0, 8, 0)
                            },
                            new Button { Content = "확인", IsDefault = true }
                        }
                    }
                }
            }
        };

        var buttons = ((StackPanel)dialog.Content!).Children.OfType<StackPanel>().Last();
        var cancelButton = (Button)buttons.Children[0];
        var okButton = (Button)buttons.Children[1];
        var accepted = false;

        cancelButton.Click += (_, _) => dialog.Close();
        okButton.Click += (_, _) =>
        {
            accepted = true;
            dialog.Close();
        };

        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
            await Task.CompletedTask;
        }

        if (!accepted || comboBox.SelectedItem is not string family)
        {
            return;
        }

        _workingSettings.MessageFontFamily = family;
        _workingSettings.MessageFontSize = (double)(sizeBox.Value ?? 13m);
        MessageFontHelper.ApplyPreview(_workingSettings, FontPreviewTextBlock);
    }

    private async void BrowseFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return;
        }

        var startFolder = await storage.TryGetFolderFromPathAsync(ReceiveDirectoryTextBox.Text ?? string.Empty);
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "수신 파일 저장 폴더 선택",
            SuggestedStartLocation = startFolder
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ReceiveDirectoryTextBox.Text = path;
        }
    }

    private void OpenShareFolderInExplorerButton_Click(object? sender, RoutedEventArgs e)
    {
        var path = ShareFolderPathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = SharedFolderPathHelper.GetDefaultShareFolderPath();
            ShareFolderPathTextBox.Text = path;
            UpdateShareFolderStatus(path);
        }

        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("file://") { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _ = DialogHelper.ShowWarningAsync(GetOwnerWindow(), "Y-popup", $"탐색기를 열 수 없습니다.\n\n{ex.Message}");
        }
    }

    private async void BrowseShareFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return;
        }

        var startFolder = await storage.TryGetFolderFromPathAsync(ShareFolderPathTextBox.Text ?? string.Empty);
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "공유할 폴더 선택",
            SuggestedStartLocation = startFolder
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ShareFolderPathTextBox.Text = path;
            UpdateShareFolderStatus(path);
        }
    }
}
