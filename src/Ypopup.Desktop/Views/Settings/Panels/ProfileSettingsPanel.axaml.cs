using Avalonia.Controls;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class ProfileSettingsPanel : UserControl
{
    public ProfileSettingsPanel()
    {
        InitializeComponent();
    }

    public void Load(AppSettings settings)
    {
        DisplayNameTextBox.Text = settings.DisplayName;
        GroupTextBox.Text = settings.Group;
        EmailTextBox.Text = settings.Email;
        MemoTextBox.Text = settings.Memo;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.DisplayName = DisplayNameTextBox.Text?.Trim() ?? string.Empty;
        settings.Group = GroupTextBox.Text?.Trim() ?? string.Empty;
        settings.Email = EmailTextBox.Text?.Trim() ?? string.Empty;
        settings.Memo = MemoTextBox.Text?.Trim() ?? string.Empty;
    }
}
