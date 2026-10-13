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
            {
                if (Clothes.Worn(_save, slot) is not { } g) continue;
                w += g.SlotId switch
                {
                    "shirt" => g.Fluffy ? 2 : g.LongSleeves ? 1 : 0,
                    "hat" => g.Id is "beanie" or "nightcap" ? 1 : 0,
                    "neck" => g.Id.StartsWith("scarf") ? 1 : 0,
                    "gloves" => g.Fluffy || g.Id is "mittens" or "mint_mittens" or "winter" ? 1 : 0,
                    "shoes" => g.Id is "boots" ? 1 : 0,
                    _ => 0,
                };
            }
            return w;
        }
    }

    /// <summary>
    /// Колко му е студено: 0 – над 15°C, 1 – хладно (10–15), 2 – студено (5–10),
    /// 3 – много студено (0–5), 4 – мраз (под 0). Колкото по-студено, толкова
    /// по-топли дрехи иска (Warmth ≥ нивото), а от 2 нагоре – и печицата.
    /// </summary>
    private int ColdLevel => _tempC switch
    {
        null => 0,
        >= Weather.Cool => 0,
        >= 10 => 1,
        >= 5 => 2,
        >= 0 => 3,
        _ => 4,
    };

    private bool Cold => ColdLevel > 0;
    private bool Hot => _tempC > Weather.Hot;
    private bool WantsHeater => ColdLevel >= 2;
    private bool DressedWarmEnough => Warmth >= ColdLevel;
    private bool Comfortable => DressedWarmEnough && (!WantsHeater || _save.HeaterOn);

    /// <summary>За менюто: „7°C · студено му е“.</summary>
    public string? WeatherText
    {
        get
        {
            if (_tempC is not { } t) return null;
            string feel;
            if (Hot) feel = Warmth >= 2 ? "горещо му е в тези дрехи" : "топло е, но е облечен леко";
            else if (!Cold) feel = "приятно му е";
            else
            {
                string how = ColdLevel switch { 1 => "хладно е", 2 => "студено е", 3 => "много е студено", _ => "мраз е" };
                if (Comfortable) feel = $"{how}, но му е топличко";
                else if (!DressedWarmEnough && WantsHeater && !_save.HeaterOn) feel = $"{how} – иска печицата и по-топли дрехи";
                else if (!DressedWarmEnough) feel = $"{how} – иска по-топли дрехи";
                else feel = $"{how} – иска печицата";
            }
            return $"🌡 {t:0}°C · {feel}";
        }
    }

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
        if (now < _nextWarmthLine || _tempC == null) return;
        // Колкото му е по-неудобно, толкова по-често казва (40–80 мин, а в мраза без дрехи – 15–30).
        int gap = Comfortable ? 40 : ColdLevel >= 3 ? 15 : ColdLevel == 2 ? 25 : 35;
        _nextWarmthLine = now + gap * 60 + _rng.Next(gap * 60);
        if (IsQuiet || InFocus || IsResting || Cooks || _state is BearState.Drag or BearState.Falling) return;
        TalkAboutWeather();
    }

    /// <summary>Казва нещо за студа/топлото според нивото и какво му липсва.</summary>
    private void TalkAboutWeather()
    {
        string o = _save.OwnerName;
        if (Cold)
        {
            int lvl = ColdLevel;
            if (WantsHeater && !Kitchen.Owns(_save, Kitchen.Heater))
            {
                Say(Lines.Pick(lvl >= 3 ? Lines.FreezingNoHeater : Lines.ColdNoHeater, o), 7);
                return;
            }
            if (WantsHeater && !_save.HeaterOn)
            {
                Say(Lines.Pick(Lines.HeaterPlease, o) + " (цъкни ме)", 8);
                _heaterAskUntil = Now + 120;
                return;
            }
            if (!DressedWarmEnough)
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
            Say(Lines.Pick(lvl >= 2 ? Lines.WarmInWinter : Lines.CoolButFine, o), 4);
            return;
        }
        if (Hot)
        {
            if (_save.HeaterOn)
            {
                SetHeater(false);
                Say(Lines.Pick(Lines.HotHeaterOff, o), 4);
            }
            else if (Warmth >= 2)
                Say(Lines.Pick(Lines.HotTooDressed, o), 6);
            else
                Say(Lines.Pick(Lines.HotButFine, o), 4);
            return;
        }
        if (_save.HeaterOn && _tempC >= Weather.Cool + 3)
        {
            SetHeater(false);
            Say(R.Any("weather.heateroff"), 4);
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
            new ChatOption("🔥 Включи я", p => { p.SetHeater(true); p.Say(Lines.Fill(R.Any("heater.on"), p.Save.OwnerName), 4); }),
            new ChatOption("Не сега", p => p.Say(R.Any("heater.notnow"), 3))));
        return true;
    }

    /// <summary>Включва/изключва печицата (ако е купена).</summary>
    public void SetHeater(bool on)
    {
        if (on && !Kitchen.Owns(_save, Kitchen.Heater)) return;
        _save.HeaterOn = on;
        Persist();
        NotifyCare();
    }

    /// <summary>Печицата на земята: показва се, докато е включена, и свети (два кадъра).</summary>
    private void UpdateHeater(double now)
    {
        bool on = _save.HeaterOn && Kitchen.Owns(_save, Kitchen.Heater);
        if (!on)
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
            _heaterWindow = new CookerWindow("Печица") { Hint = "Печицата грее. Хвани я, за да я преместиш; цъкни, за да я изгасиш." };
            _heaterWindow.SetTime(null);
            _heaterWindow.Clicked += () => Ask(new ChatQuestion("Да изключа ли печицата?",
                new ChatOption("Изключи я", p => p.SetHeater(false)),
                new ChatOption("Не, нека грее", p => p.Say(R.Any("heater.keep"), 3))));
            _heaterWindow.Dropped += (cx, _) =>
            {
                var a = AreaAt(cx, _heaterWindow!.Top + _heaterWindow.ActualHeight / 2);
                double nx = Math.Clamp(cx, a.Left + 30, a.Right - 30);
                _heaterWindow.FallTo(nx, a.Bottom);
                _save.HeaterX = nx;
                Persist();
            };
            _heaterWindow.SetAway(!IsVisible);
            _heaterWindow.Place(_lib.GetProp("heater", 20, 22, 0).Image, PixelSize, x, area.Bottom);
            _save.HeaterX = x;
        }
        _heaterWindow.Step(0.033);
        if (now > _nextHeaterFrame)
        {
            _nextHeaterFrame = now + 0.4;
            _heaterWindow.SetArt(_lib.GetProp((int)(now / 0.4) % 2 == 0 ? "heater" : "heater_b", 20, 22, 0).Image, PixelSize);
        }
    }

    /// <summary>Седи до печицата и се топли (когато е включена).</summary>
    private void WarmUpByHeater()
    {
        if (_heaterWindow == null) return;
        double cx = _heaterWindow.CenterX;
        int side = _x < cx ? -1 : 1;
        double spot = cx + side * (SpriteWidth * 0.55 + 10 * PixelSize);
        WalkTo(spot, () =>
        {
            StartScene(Scenes.Sit, quiet: true);
            _facingLeft = side > 0;
            _sceneUntil = Now + 120 + _rng.Next(180);
            Announce(Lines.Pick(Lines.HeaterLove, _save.OwnerName), 4);
        });
    }
}
