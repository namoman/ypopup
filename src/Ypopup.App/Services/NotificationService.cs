using System.Media;
using System.Runtime.InteropServices;
using Ypopup.Core.Models;

namespace Ypopup.App.Services;

public static class NotificationService
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBeep(uint type);

    public static void PlayMessageReceived(AppSettings settings)
    {
        if (!settings.SoundEnabled || !settings.PlayMessageReceivedSound)
        {
            return;
        }

        PlayDefaultSound();
    }

    public static void PlayFileReceived(AppSettings settings)
    {
        if (!settings.SoundEnabled || !settings.PlayFileReceivedSound)
        {
            return;
        }

        PlayDefaultSound();
    }

    private static void PlayDefaultSound()
    {
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
