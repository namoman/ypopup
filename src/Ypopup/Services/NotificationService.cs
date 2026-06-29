using System.Media;
using System.Runtime.InteropServices;
using Ypopup.Models;

namespace Ypopup.Services;

public static class NotificationService
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBeep(uint type);

    public static void PlayNotificationSound(bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch (Exception)
        {
            MessageBeep(0x00000040);
        }
    }
}
