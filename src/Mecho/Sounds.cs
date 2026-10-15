using System;
using System.IO;
using System.Windows.Media;

namespace Mecho;

/// <summary>
/// Звуците (засега само „динг“ – когато свърши мечо-доро, почивка или таймер).
/// Файловете са в assets/sounds; включват се и се усилват от „Още“ → „Звук“.
/// </summary>
public static class Sounds
{
    private static MediaPlayer? _player;

    public static void Play(SaveData save, string name = "ding")
    {
        if (!save.SoundOn || save.SoundVolume <= 0) return;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "assets", "sounds", name + ".wav");
            if (!File.Exists(path)) return;
            _player ??= new MediaPlayer();
            _player.Open(new Uri(path));
            _player.Volume = Math.Clamp(save.SoundVolume, 0, 1);
            _player.Play();
        }
        catch (Exception)
        {
            // Без звук – не е страшно.
        }
    }
}
