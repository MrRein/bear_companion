using System;

namespace Mecho;

// Лампата: купена ли е, вечер (след залез) мечокът я пали и тя стои на земята
// до него и свети. Тут може да я мести и да я изгаси (до сутринта). Като
// легне за деня, я гаси сам.
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
    public bool LampOn =>
        Kitchen.Owns(_save, Kitchen.Lamp) && IsDark(DateTime.Now) && !SleepsForDay &&
        !(_save.LampOff && _save.LampOffDay == EveningKey(DateTime.Now));

    /// <summary>Тут я изгаси (или пак я светна).</summary>
    public void SetLamp(bool on)
    {
        if (!Kitchen.Owns(_save, Kitchen.Lamp)) return;
        _save.LampOff = !on;
        _save.LampOffDay = EveningKey(DateTime.Now);
        Persist();
        NotifyCare();
    }

    private void UpdateLamp()
    {
        bool on = LampOn;
        if (on != _lampWasOn)
        {
            _lampWasOn = on;
            // Сам я пали, като се стъмни (и казва нещо, ако е буден и не е зает).
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
            _lampWindow.SetTime(null);
            _lampWindow.Hint = "Лампата свети.";
            _lampWindow.Clicked += () => Ask(new ChatQuestion("Да изгася ли лампата?",
                new ChatOption("Изгаси я", p => { p.SetLamp(false); p.Say(R.Pick("lamp.off", p.Save.OwnerName), 3); }),
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
