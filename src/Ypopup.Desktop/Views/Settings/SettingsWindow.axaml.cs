using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Settings;

public partial class SettingsWindow : Window
{
    private SettingsEditor? _editor;

    public SettingsWindow(YpopupCoordinator coordinator)
    {
        InitializeComponent();
        _editor = new SettingsEditor(coordinator);
        _editor.LoadIntoPanels(ProfilePanel, NetworkPanel, GeneralPanel, AwayPanel);
    }

    private void SettingsTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        if (SettingsTabControl.SelectedItem is TabItem { Header: "네트워크" })
        {
            ProfilePanel.ApplyTo(_editor.WorkingSettings);
            GeneralPanel.ApplyTo(_editor.WorkingSettings);
            AwayPanel.ApplyTo(_editor.WorkingSettings);
            NetworkPanel.RefreshFirewallStatus();
        }
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        if (await _editor.TrySaveAsync(this, ProfilePanel, NetworkPanel, GeneralPanel, AwayPanel))
        {
            Close();
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close();
}
