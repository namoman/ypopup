using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Ypopup.Core.Logging;
using Ypopup.Core.Text;

namespace Ypopup.Desktop.Controls;

public class MessageBodyText : SelectableTextBlock
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly IBrush LinkOnLight = new SolidColorBrush(Color.Parse("#2563EB"));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<MessageBodyText, string?>(nameof(Message));

    private Point _pressPoint;
    private bool _linkPress;

    static MessageBodyText()
    {
        MessageProperty.Changed.AddClassHandler<MessageBodyText>((control, _) => control.Rebuild());
        ForegroundProperty.Changed.AddClassHandler<MessageBodyText>((control, _) => control.Rebuild());
    }

    public MessageBodyText()
    {
        TextWrapping = TextWrapping.Wrap;
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = LinkIndex(e.GetPosition(this)) is null ? null : HandCursor;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Cursor = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _pressPoint = e.GetPosition(this);
        _linkPress = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && LinkIndex(_pressPoint) is not null;
        if (_linkPress)
        {
            e.Handled = true;
            return;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_linkPress || e.InitialPressMouseButton != MouseButton.Left)
        {
            base.OnPointerReleased(e);
            return;
        }

        _linkPress = false;
        e.Handled = true;
        var end = e.GetPosition(this);
        if (Math.Abs(end.X - _pressPoint.X) > 4 || Math.Abs(end.Y - _pressPoint.Y) > 4)
        {
            return;
        }

        if (LinkIndex(end) is not int index)
        {
            return;
        }

        var link = MessageLinkFinder.At(Message, index);
        var target = link is null ? null : MessageLinkFinder.ToLaunchTarget(link.Value.Target);
        if (target is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Warning("MessageLink", $"Open link: {ex.Message}");
        }
    }

    private void Rebuild()
    {
        Inlines?.Clear();
        var textBrush = Foreground ?? Brushes.Black;
        var linkBrush = IsLight(textBrush) ? Brushes.White : LinkOnLight;
        foreach (var part in MessageLinkFinder.Split(Message))
        {
            var run = new Run(part.Text)
            {
                Foreground = part.LinkTarget is null ? textBrush : linkBrush,
            };
            if (part.LinkTarget is not null)
            {
                run.TextDecorations = Avalonia.Media.TextDecorations.Underline;
            }

            Inlines?.Add(run);
        }
    }

    private int? LinkIndex(Point point)
    {
        if (TextLayout is null)
        {
            return null;
        }

        var hit = TextLayout.HitTestPoint(point);
        if (!hit.IsInside)
        {
            return null;
        }

        var index = hit.IsTrailing ? hit.TextPosition - 1 : hit.TextPosition;
        if (index < 0 || MessageLinkFinder.At(Message, index) is null)
        {
            return null;
        }

        return index;
    }

    private static bool IsLight(IBrush brush)
    {
        return brush is SolidColorBrush solid
            && solid.Color.R > 200
            && solid.Color.G > 200
            && solid.Color.B > 200;
    }
}
