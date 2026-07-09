using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Ypopup.Desktop.Controls;

public partial class TransferProgressBar : UserControl
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<TransferProgressBar, double>(nameof(Progress), 0);

    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<TransferProgressBar, string?>(nameof(FileName));

    public static readonly StyledProperty<bool> IsCancellableProperty =
        AvaloniaProperty.Register<TransferProgressBar, bool>(nameof(IsCancellable), true);

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<TransferProgressBar, ICommand?>(nameof(CancelCommand));

    public event EventHandler? CancelRequested;

    public TransferProgressBar()
    {
        InitializeComponent();
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string? FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    public bool IsCancellable
    {
        get => GetValue(IsCancellableProperty);
        set => SetValue(IsCancellableProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (CancelCommand?.CanExecute(null) == true)
        {
            CancelCommand.Execute(null);
        }
        CancelRequested?.Invoke(this, e);
    }
}