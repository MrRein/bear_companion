using System;

namespace Mecho;

// Лампата: Тут я слага („Неща на Мечо“ в менюто) и тя стои на земята до мечока и
// свети, докато Тут не я прибере (пак от „Неща на Мечо“ или с клик върху нея). Може да се мести.
public sealed partial class PetWindow
{
    private CookerWindow? _lampWindow;
    private bool _lampWasOn;

    /// <summary>Кога се стъмва (приблизително, по месеца): зимата в 17 ч, лятото в 20:30.</summary>
    public static double DuskHour(int month) => month switch
    {
        12 or 1 => 17,
        2 or 11 => 17.5,
        3 or 10 => 18.5,
        4 or 9 => 19.5,
        5 or 8 => 20,
        _ => 20.5,
    };

    /// <summary>Тъмно е навън (от залез до 6 ч сутринта).</summary>
    public static bool IsDark(DateTime t) => t.Hour < 6 || t.Hour + t.Minute / 60.0 >= DuskHour(t.Month);

    /// <summary>„Вечерта“, за която е изгасена: след полунощ още е вчерашната.</summary>
    private static string EveningKey(DateTime t) => (t.Hour < 6 ? t.Date.AddDays(-1) : t.Date).ToString("yyyy-MM-dd");

    /// <summary>Лампата свети: купена, тъмно е, не е изгасена тази вечер и мечокът не е легнал за деня.</summary>
    public bool LampOn => Kitchen.Owns(_save, Kitchen.Lamp) && _save.LampPlaced;

    /// <summary>Тут я сложи (или прибра).</summary>
    public void SetLamp(bool on)
    {
        if (!Kitchen.Owns(_save, Kitchen.Lamp)) return;
        if (on && !_save.LampPlaced) _save.LampX = double.NaN;   // до мечока, не там, където е била
        _save.LampPlaced = on;
        Persist();
        NotifyCare();
    }

    private double _nextLampAsk = 5 * 60;

    /// <summary>
    /// Вечер (след 20 ч, тъмно е), има лампа, но не е сложена, а той прави нещо
    /// (работи, чете, хоби…): понякога (най-много веднъж на вечер) моли да му я сложиш.
    /// </summary>
    private void MaybeAskForLamp()
    {
        if (Now < _nextLampAsk) return;
        _nextLampAsk = Now + Minutes(10, 20);
        var now = DateTime.Now;
        if (!Kitchen.Owns(_save, Kitchen.Lamp) || LampOn || !IsDark(now) || now.Hour is < 20 and >= 6) return;
        if (_save.LampOffDay == EveningKey(now)) return;          // тази вечер вече пита
        bool busy = _inScene || InFocus || _state == BearState.Busy;
        if (!busy || IsAsleep || IsQuiet || OnWalk || ChatIsOpen || MenuIsOpen || _rng.Next(3) != 0) return;
        _save.LampOffDay = EveningKey(now);
        Persist();
        Ask(new ChatQuestion(R.Pick("lamp.ask", _save.OwnerName),
            new ChatOption("Слагам я", p => { p.SetLamp(true); p.Say(R.Pick("lamp.thanks", p.Save.OwnerName), 3); }),
            new ChatOption("Не сега", p => p.Say(R.Pick("lamp.nothanks", p.Save.OwnerName), 3))));
    }

    private void UpdateLamp()
    {
        MaybeAskForLamp();
        bool on = LampOn;
        if (on != _lampWasOn)
        {
            _lampWasOn = on;
            // Тут я сложи: казва нещо, ако е буден и не е зает.
            if (on && !IsAsleep && !InFocus && !IsQuiet) Mutter(R.Pick("lamp.on", _save.OwnerName), 4);
        }
        if (!on)
        {
            if (_lampWindow != null)
            {
                _lampWindow.Close();
                _lampWindow = null;
            }
            return;
        }
        if (_lampWindow == null)
        {
            var area = Area;
            double x = double.IsNaN(_save.LampX) ? _x + (_x > area.Left + area.Width / 2 ? -1 : 1) * SpriteWidth * 1.2 : _save.LampX;
            x = Math.Clamp(x, area.Left + 30, area.Right - 30);
            _lampWindow = new CookerWindow("Лампа");
            Companion(_lampWindow, Kitchen.Lamp);
            _lampWindow.SetTime(null);
            _lampWindow.Hint = "Лампата свети.";
            _lampWindow.Clicked += () => Ask(new ChatQuestion("Да прибера ли лампата?",
                new ChatOption("Прибери я", p => { p.SetLamp(false); p.Say(R.Pick("lamp.off", p.Save.OwnerName), 3); }),
                new ChatOption("Не, нека свети", p => p.Say(R.Pick("lamp.keep", p.Save.OwnerName), 3))));
            _lampWindow.Dropped += (cx, _) =>
            {
                var a = AreaAt(cx, _lampWindow!.Top + _lampWindow.ActualHeight / 2);
                double nx = Math.Clamp(cx, a.Left + 30, a.Right - 30);
                _lampWindow.FallTo(nx, a.Bottom);
                _save.LampX = nx;
                Persist();
            };
            _lampWindow.SetAway(!IsVisible);
            _lampWindow.Place(_lib.GetProp(Furniture.CurrentArt(_save, Kitchen.Lamp), 14, 30, 0).Image, PixelSize, x, area.Bottom);
            _save.LampX = x;
        }
        _lampWindow.Step(0.033);
    }
}
