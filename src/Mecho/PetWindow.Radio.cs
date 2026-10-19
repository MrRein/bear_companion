using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Радиото (купува се в „Хобита“): Тут го слага от „Неща на Мечо“. Може да е включено
// или изключено. Включеното подскача леко и от него излизат нотки, а мечокът –
// ако има енергия и не работи – понякога отива до него и танцува. Цъкнеш ли го:
// Изключи/Включи, Махни, Остави. Скинове: грамофон, касетофон, лампово радио, джубокс.
public sealed partial class PetWindow
{
    private CookerWindow? _radioWindow;
    private OverlayWindow? _notesWindow;
    private readonly Canvas _notesCanvas = new() { Width = 60, Height = 60, IsHitTestVisible = false };
    private readonly Image[] _notes = new Image[3];

    public bool RadioPlaced => Kitchen.Owns(_save, Kitchen.Radio) && _save.RadioPlaced;

    /// <summary>Сложено и включено – свири.</summary>
    public bool RadioPlaying => RadioPlaced && _save.RadioOn;

    /// <summary>Тут го сложи (включено) или прибра.</summary>
    public void SetRadio(bool placed)
    {
        if (!Kitchen.Owns(_save, Kitchen.Radio)) return;
        if (placed && !_save.RadioPlaced) _save.RadioX = double.NaN;   // до мечока, не там, където е било
        _save.RadioPlaced = placed;
        if (placed) _save.RadioOn = true;
        Persist();
        NotifyCare();
    }

    public void SetRadioOn(bool on)
    {
        _save.RadioOn = on;
        Persist();
        NotifyCare();
    }

    private void UpdateRadio(double now)
    {
        if (!RadioPlaced)
        {
            _radioWindow?.Close();
            _radioWindow = null;
            _notesWindow?.Conceal();
            return;
        }
        if (_radioWindow == null)
        {
            var area = Area;
            double x = double.IsNaN(_save.RadioX) ? _x + (_x > area.Left + area.Width / 2 ? -1 : 1) * SpriteWidth * 1.8 : _save.RadioX;
            x = Math.Clamp(x, area.Left + 30, area.Right - 30);
            _radioWindow = new CookerWindow("Радио");
            Companion(_radioWindow, Kitchen.Radio);
            _radioWindow.SetTime(null);
            _radioWindow.Hint = "Радиото.";
            _radioWindow.Clicked += () => Ask(new ChatQuestion(RadioPlaying ? "Да спра ли музиката?" : "Да пусна ли музика?",
                new ChatOption(RadioPlaying ? "Изключи" : "Включи", p => { p.SetRadioOn(!p.Save.RadioOn); p.Say(R.Pick(p.Save.RadioOn ? "radio.on" : "radio.off", p.Save.OwnerName), 3); }),
                new ChatOption("Махни", p => p.SetRadio(false)),
                new ChatOption("Остави", _ => { })));
            _radioWindow.Dropped += (cx, _) =>
            {
                var a = AreaAt(cx, _radioWindow!.Top + _radioWindow.ActualHeight / 2);
                double nx = Math.Clamp(cx, a.Left + 30, a.Right - 30);
                _radioWindow.FallTo(nx, a.Bottom);
                _save.RadioX = nx;
                Persist();
            };
            _radioWindow.SetAway(!IsVisible);
            _radioWindow.Place(RadioArt, PixelSize, x, area.Bottom);
            _save.RadioX = x;
        }
        _radioWindow.Step(0.033);
        // Свири: подскача в ритъм и пуска нотки.
        bool playing = RadioPlaying && IsVisible && !_radioWindow.IsDragging && !_radioWindow.IsFalling;
        _radioWindow.Shake(playing ? Math.Round(Math.Sin(now * 14)) * PixelSize * 0.5 : 0);
        UpdateNotes(now, playing);
    }

    private System.Windows.Media.Imaging.BitmapSource RadioArt => _lib.GetProp(Furniture.CurrentArt(_save, Kitchen.Radio), 14, 12, 0).Image;

    /// <summary>Три нотки се издигат над радиото и избледняват (на кръг, всяка с отместване).</summary>
    private void UpdateNotes(double now, bool playing)
    {
        if (!playing || _radioWindow == null)
        {
            _notesWindow?.Conceal();
            return;
        }
        if (_notesWindow == null)
        {
            var icon = Ui.LoadIcon("music");
            for (int i = 0; i < _notes.Length; i++)
            {
                _notes[i] = new Image { Source = icon, Width = 12 * 1.5, Height = 12 * 1.5, IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(_notes[i], BitmapScalingMode.NearestNeighbor);
                _notesCanvas.Children.Add(_notes[i]);
            }
            _notesWindow = new OverlayWindow(_notesCanvas);
            Companion(_notesWindow);
        }
        for (int i = 0; i < _notes.Length; i++)
        {
            double t = (now * 0.5 + i / 3.0) % 1.0;   // 0 → 1 за 2 секунди
            Canvas.SetLeft(_notes[i], 21 + Math.Sin((t + i) * 6) * 14);
            Canvas.SetTop(_notes[i], 42 - t * 42);
            _notes[i].Opacity = t < 0.15 ? t / 0.15 : 1 - (t - 0.15) / 0.85;
        }
        _notesWindow.Place(_radioWindow.CenterX - 30, _radioWindow.Top - 50);
    }

    /// <summary>Свири радиото: отива до него и танцува (Слушай музика), докато не му омръзне.</summary>
    private void DanceByRadio()
    {
        if (_radioWindow == null || !RadioPlaying) return;
        // Сам отива до радиото (StartScene → WalkToRadioFirst).
        StartScene(Scenes.Music, 45 + _rng.Next(75));
    }
}
