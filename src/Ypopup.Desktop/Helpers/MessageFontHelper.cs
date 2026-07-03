using Avalonia.Controls;
using Avalonia.Media;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Helpers;

public static class MessageFontHelper
{
    public static void Apply(AppSettings settings, params TextBox[] textBoxes)
    {
        var family = new FontFamily(settings.MessageFontFamily);
        var fontSize = settings.MessageFontSize > 0 ? settings.MessageFontSize : 13;

        foreach (var textBox in textBoxes)
        {
            textBox.FontFamily = family;
            textBox.FontSize = fontSize;
        }
    }

    public static void ApplyPreview(AppSettings settings, TextBlock preview)
    {
        preview.FontFamily = new FontFamily(settings.MessageFontFamily);
        preview.FontSize = settings.MessageFontSize > 0 ? settings.MessageFontSize : 13;
        preview.Text = "ex> 가나다 abc 123";
    }
}
