using System;
using System.Linq;

namespace Mecho;

// Студ и топло: температурата навън (Weather.cs). Под 10°C му е студено – иска
// пуловер/шал и печицата (ако я няма – пита дали да си вземете); над 25°C му е
// горещо – иска да свали пуловера. Облечен ли е подходящо – доволен е.
// Печицата е предмет на земята (като уреда), който свети; той обича да седи до нея.
public sealed partial class PetWindow
{
    private double? _tempC;
    private double _nextWeather = 15, _nextWarmthLine = 300, _heaterAskUntil, _nextHeaterFrame;
    private bool _weatherBusy;
    private CookerWindow? _heaterWindow;

    public double? Temperature => _tempC;

    /// <summary>Колко топло е облечен: пуловер 2, блуза с ръкав 1, шапка, шал, плетени ръкавички, ботуши по 1.</summary>
    private int Warmth
    {
        get
        {
            int w = 0;
            foreach (var slot in Clothes.Slots)
                if (Clothes.Worn(_save, slot) is { } g) w += WarmthOf(g);
            return w;
        }
    }

    private static int WarmthOf(Garment g) => g.SlotId switch
    {
        "shirt" => g.Fluffy ? 2 : g.LongSleeves ? 1 : 0,
        "hat" => g.Id is "beanie" or "nightcap" ? 1 : 0,
        "neck" => g.Id.StartsWith("scarf") ? 1 : 0,
        "gloves" => g.Fluffy || g.Id is "mittens" or "mint_mittens" or "winter" ? 1 : 0,
        "shoes" => g.Id is "boots" ? 1 : 0,
        _ => 0,
    };

    /// <summary>Колко топло би бил, ако облече най-топлото, което има (по едно за всяко място).</summary>
    private int BestWarmth => Clothes.Slots.Sum(slot =>
        Clothes.All.Where(g => g.SlotId == slot.Id && Clothes.Owns(_save, g)).Select(WarmthOf).DefaultIfEmpty(0).Max());

    /// <summary>
    /// Колко му е студено (по закръглените градуси): 0 – над 15°C, 1 – хладно (11–15),
    /// 2 – студено (6–10), 3 – много студено (1–5), 4 – мраз (0 и под). Колкото по-студено,
    /// толкова по-топли дрехи иска (Warmth ≥ нивото), а от 2 нагоре – и печицата.
    /// </summary>
    private int ColdLevel => _tempC is not { } t ? 0 : Math.Round(t) switch
    {
        > 15 => 0,
        > 10 => 1,
        > 5 => 2,
        > 0 => 3,
        _ => 4,
    };

    private bool Cold => ColdLevel > 0;
    private bool Hot => _tempC is { } t && Math.Round(t) >= Weather.Hot;
    private bool VeryHot => _tempC is { } t && Math.Round(t) >= Weather.VeryHot;
    private bool WantsHeater => ColdLevel >= 2;
    private bool DressedWarmEnough => Warmth >= ColdLevel;
    private bool Comfortable => DressedWarmEnough && (!WantsHeater || _save.HeaterOn);

    /// <summary>За менюто: „7°C · студено му е“.</summary>
    public string? WeatherText
    {
        get
        {
            if (_tempC is not { } t) return null;
            // Само как е навън – без какво точно иска.
            string feel = VeryHot ? "жега е" : Hot ? "горещо е" : !Cold ? (Math.Round(t) <= 20 ? "хладничко-приятно е" : "топло е")
                : ColdLevel switch { 1 => "хладно е", 2 => "студено е", 3 => "много е студено", _ => "мраз е" };
            string sky = Weather.KindText(t) is { } k ? $", {k}" : "";
            return $"🌡 {t:0}°C{sky} · {feel}";
        }
    }

    /// <summary>За менюто: иконка на небето и градусите (null – още не знаем времето).</summary>
    public (string Icon, string Temp)? WeatherBadge =>
        _tempC is { } t ? (Weather.SkyIcon(DateTime.Now), $"{t:0}°C") : null;

    private void UpdateWeather(double now)
    {
        if (now > _nextWeather && !_weatherBusy)
        {
            _nextWeather = now + 30 * 60;
            _weatherBusy = true;
            _ = Weather.FetchAsync(_save).ContinueWith(t => Dispatcher.BeginInvoke(() =>
            {
                _weatherBusy = false;
                if (t.Result is { } temp)
                {
                    _tempC = temp;
                    NotifyCare();
                }
            }));
        }
        UpdateHeater(now);
        UpdateLamp();
        UpdateRadio(now);
        AskHeaterCloser(now);
        UpdateItems(now);   // предметите на земята: понякога прави нещо с тях (Items/*.cs)
        if (now < _nextWarmthLine || _tempC == null) return;
        // Колкото му е по-неудобно, толкова по-често казва (40–80 мин, а в мраза без дрехи – 15–30).
        int gap = Comfortable ? 100 : ColdLevel >= 3 ? 45 : ColdLevel == 2 ? 60 : 80;
        _nextWarmthLine = now + gap * 60 + _rng.Next(gap * 60);
        if (IsQuiet || InFocus || IsResting || Cooks || _state is BearState.Drag or BearState.Falling) return;
        TalkAboutWeather();
    }

    private double _nextCloserLine = 600;

    /// <summary>
    /// Прави нещо (чете, работи си…), студено му е, а печицата грее далече:
    /// понякога казва „Доближи печицата до мен“ (на 12–25 мин).
    /// </summary>
    private void AskHeaterCloser(double now)
    {
        if (now < _nextCloserLine) return;
        _nextCloserLine = now + Minutes(12, 25);
        if (_heaterWindow == null || !Cold || !_inScene || InFocus || IsQuiet || IsResting) return;
        if (Math.Abs(_heaterWindow.CenterX - _x) < SpriteWidth * 2) return;
        Say(R.Pick("heater.closer", _save.OwnerName), 5);
    }

    /// <summary>Казва нещо за студа/топлото според нивото и какво му липсва.</summary>
    private void TalkAboutWeather()
    {
        string o = _save.OwnerName;
        if (Cold)
        {
            int lvl = ColdLevel;
            // Няма печица: моли за нея само ако е в избора/целта в магазина и имате
            // лешници за нея (и то не винаги); иначе просто говори за времето.
            if (WantsHeater && !Kitchen.Owns(_save, Kitchen.Heater))
            {
                int price = Kitchen.Find(Kitchen.Heater)?.Price ?? 30;
                bool offered = Goal?.Id == Kitchen.Heater || GoalChoices.Any(u => u.Id == Kitchen.Heater);
                if (offered && _save.Hazelnuts >= price && _rng.Next(2) == 0) Say(Lines.Pick(lvl >= 3 ? Lines.FreezingNoHeater : Lines.ColdNoHeater, o), 7);
                else Say(R.Pick(lvl >= 2 ? "weather.cold" : "weather.cool", o), 4);
                return;
            }
            if (WantsHeater && !_save.HeaterOn)
            {
                // Печицата е сложена, но изключена: отива и я пуска сам (Items/HeaterItem.cs).
                if (_save.HeaterPlaced && TryItem(Kitchen.Heater, "turn_on")) return;
                Say(Lines.Pick(Lines.HeaterPlease, o), 8);
                _heaterAskUntil = Now + 120;
                return;
            }
            // Иска по-топли дрехи само ако има такива в гардероба (иначе – за времето).
            if (!DressedWarmEnough && BestWarmth > Warmth)
            {
                Say(Lines.Pick(lvl switch
                {
                    1 => Lines.CoolWantsSleeves,
                    2 => Lines.ColdWantsSweater,
                    3 => Lines.VeryColdWantsMore,
                    _ => Lines.FrostWantsEverything,
                }, o), 6);
                return;
            }
            if (!DressedWarmEnough)
            {
                Say(R.Pick(lvl >= 2 ? "weather.cold" : "weather.cool", o), 4);
                return;
            }
            Say(Lines.Pick(lvl >= 2 ? Lines.WarmInWinter : Lines.CoolButFine, o), 4);
            return;
        }
        if (Hot)
        {
            if (_save.HeaterOn)
            {
                // Отива и я спира сам; ако не може сега – спира я от разстояние.
                if (!TryItem(Kitchen.Heater, "turn_off"))
                {
                    SetHeater(false);
                    Say(Lines.Pick(Lines.HotHeaterOff, o), 4);
                }
            }
            else if (Warmth >= 2)
                Say(Lines.Pick(Lines.HotTooDressed, o), 6);
            else
                Say(Lines.Pick(Lines.HotButFine, o), 4);
            return;
        }
        if (_save.HeaterOn && _tempC >= Weather.Cool + 3)
        {
            if (!TryItem(Kitchen.Heater, "turn_off"))
            {
                SetHeater(false);
                Say(R.Any("weather.heateroff"), 4);
            }
            return;
        }
        if (Warmth >= 3)
        {
            Say(Lines.Pick(Lines.MildTooDressed, o), 5);
            return;
        }
        Say(Lines.Pick(Lines.MildWeather, o), 4);
    }

    /// <summary>Клик скоро след „Ще ми включиш ли печицата?“.</summary>
    private bool TakeHeaterQuestion()
    {
        if (Now > _heaterAskUntil) return false;
        _heaterAskUntil = 0;
        Ask(new ChatQuestion("Ще ми включиш ли печицата?",
            new ChatOption("🌡 Включи я", p => { p.SetHeater(true); p.Say(Lines.Fill(R.Any("heater.on"), p.Save.OwnerName), 4); }),
            new ChatOption("Не сега", p => p.Say(R.Any("heater.notnow"), 3))));
        return true;
    }

    /// <summary>Включва/изключва печицата (ако е купена; включена – и сложена на земята).</summary>
    public void SetHeater(bool on)
    {
        if (on && !Kitchen.Owns(_save, Kitchen.Heater)) return;
        _save.HeaterOn = on;
        Persist();
        NotifyCare();
    }

    /// <summary>Тут я слага (включена) или прибира („Сложи“ в менюто).</summary>
    public void SetHeaterPlaced(bool placed)
    {
        if (placed && !Kitchen.Owns(_save, Kitchen.Heater)) return;
        if (placed && !_save.HeaterPlaced) _save.HeaterX = double.NaN;   // до мечока, не там, където е била
        if (placed) _save.HeaterOn = true;
        else _save.HeaterPlaced = false;
        Persist();
        NotifyCare();
    }

    /// <summary>Печицата на земята: показва се, докато е включена, и свети (два кадъра).</summary>
    private void UpdateHeater(double now)
    {
        // Печицата е на земята, докато е сложена; включена – грее (два кадъра), изключена – „_off“.
        bool placed = _save.HeaterPlaced && Kitchen.Owns(_save, Kitchen.Heater);
        bool on = placed && _save.HeaterOn;
        if (!placed)
        {
            if (_heaterWindow != null)
            {
                _heaterWindow.Close();
                _heaterWindow = null;
            }
            return;
        }
        if (_heaterWindow == null)
        {
            var area = Area;
            double x = double.IsNaN(_save.HeaterX) ? _x + (_x > area.Left + area.Width / 2 ? -1 : 1) * SpriteWidth * 1.5 : _save.HeaterX;
            x = Math.Clamp(x, area.Left + 30, area.Right - 30);
            _heaterWindow = new CookerWindow("Печица");
            Companion(_heaterWindow);
            _heaterWindow.SetTime(null);
            _heaterWindow.Hint = "Печицата.";
            _heaterWindow.Clicked += () => Ask(_save.HeaterOn
                ? new ChatQuestion("Да изключа ли печицата?",
                    new ChatOption("Изключи я", p => p.SetHeater(false)),
                    new ChatOption("Прибери я", p => p.SetHeaterPlaced(false)),
                    new ChatOption("Не, нека грее", p => p.Say(R.Any("heater.keep"), 3)))
                : new ChatQuestion("Печицата е изключена. Да я включа ли?",
                    new ChatOption("🌡 Включи я", p => { p.SetHeater(true); p.Say(Lines.Fill(R.Any("heater.on"), p.Save.OwnerName), 4); }),
                    new ChatOption("Прибери я", p => p.SetHeaterPlaced(false)),
                    new ChatOption("Остави", _ => { })));
            _heaterWindow.Dropped += (cx, _) =>
            {
                var a = AreaAt(cx, _heaterWindow!.Top + _heaterWindow.ActualHeight / 2);
                double nx = Math.Clamp(cx, a.Left + 30, a.Right - 30);
                _heaterWindow.FallTo(nx, a.Bottom);
                _save.HeaterX = nx;
                Persist();
            };
            _heaterWindow.SetAway(!IsVisible);
            _heaterWindow.Place(_lib.GetProp(HeaterArt(on, now), 20, 22, 0).Image, PixelSize, x, area.Bottom);
            _save.HeaterX = x;
        }
        _heaterWindow.Step(0.033);
        if (now > _nextHeaterFrame)
        {
            _nextHeaterFrame = now + 0.4;
            _heaterWindow.SetArt(_lib.GetProp(HeaterArt(on, now), 20, 22, 0).Image, PixelSize);
        }
    }

    /// <summary>Рисунката на печицата: включена мига (два кадъра), изключена – „_off“ (ако е нарисувана).</summary>
    private string HeaterArt(bool on, double now)
    {
        if (!on)
        {
            string off = Furniture.CurrentArt(_save, Kitchen.Heater, "_off");
            return _lib.HasProp(off) ? off : Furniture.CurrentArt(_save, Kitchen.Heater);
        }
        return Furniture.CurrentArt(_save, Kitchen.Heater, (int)(now / 0.4) % 2 == 0 ? "" : "_b");
    }

    /// <summary>Сяда до печицата (когато е включена) и се топли.</summary>
    private void WarmUpByHeater()
    {
        if (_heaterWindow == null) return;
        double cx = _heaterWindow.CenterX;
        int side = _x < cx ? -1 : 1;
        double spot = cx + side * (SpriteWidth * 0.55 + 10 * PixelSize);
        WalkTo(spot, () =>
        {
            StartScene(Scenes.Sit, quiet: true);
            _sceneNear = HeaterAnchor;   // топли се само до печицата
            _facingLeft = side > 0;
            // Колкото по-студено, толкова по-дълго седи до нея (2–5 мин, в мраза до 10).
            _sceneUntil = Now + 120 + _rng.Next(180) + ColdLevel * 75;
            Announce(Lines.Pick(Lines.HeaterLove, _save.OwnerName), 4);
        });
    }
}
