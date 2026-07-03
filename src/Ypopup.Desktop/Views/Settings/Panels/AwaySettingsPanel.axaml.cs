using Avalonia.Controls;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class AwaySettingsPanel : UserControl
{
    public AwaySettingsPanel()
    {
        InitializeComponent();
    }

    public void Load(AppSettings settings)
    {
        AwayIdleCheckBox.IsChecked = settings.AwayEnabledByIdle;
        AwayIdleMinutesTextBox.Text = settings.AwayIdleMinutes.ToString();
        AwayMessageTextBox.Text = settings.AwayMessage;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.AwayEnabledByIdle = AwayIdleCheckBox.IsChecked == true;
        if (int.TryParse(AwayIdleMinutesTextBox.Text, out var awayIdleMinutes))
        {
            settings.AwayIdleMinutes = awayIdleMinutes;
        }

        settings.AwayMessage = AwayMessageTextBox.Text?.Trim() ?? string.Empty;
    }

    public bool AwayIdleEnabled => AwayIdleCheckBox.IsChecked == true;
    public string AwayIdleMinutesText => AwayIdleMinutesTextBox.Text ?? string.Empty;
}
