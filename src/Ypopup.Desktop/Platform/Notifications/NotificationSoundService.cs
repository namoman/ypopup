using System.Runtime.InteropServices;
using Ypopup.Core.Models;

namespace Ypopup.Desktop.Platform.Notifications;

public sealed class NotificationSoundService : INotificationSoundService
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBeep(uint type);

    public void PlayMessageReceived(AppSettings settings)
    {
        if (settings.SoundEnabled && settings.PlayMessageReceivedSound)
        {
            PlayDefaultSound();
        }
    }

    public void PlayFileReceived(AppSettings settings)
    {
        if (settings.SoundEnabled && settings.PlayFileReceivedSound)
        {
            PlayDefaultSound();
        }
    }

    private static void PlayDefaultSound()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                MessageBeep(0x00000040);
                return;
            }
            catch (Exception)
            {
                // fall through
            }
        }
        try
        {
            Console.Beep(800, 200);
        }
        catch (Exception)
        {
            // no audio
        }
    }
}
