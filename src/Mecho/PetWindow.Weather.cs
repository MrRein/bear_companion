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

    /// <summary>Колко топло е облечен: пуловер 2, блуза с ръкав 1, шапка, шал, плетени ръкавички по 1.</summary>
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
                    "gloves" => g.Fluffy ? 1 : 0,
                    _ => 0,
                };
            }
            return w;
        }
    }

    private bool Cold => _tempC < Weather.Cold;
    private bool Hot => _tempC > Weather.Hot;

    /// <summary>За менюто: „7°C · студено му е“.</summary>
    public string? WeatherText
    {
        get
        {
            if (_tempC is not { } t) return null;
            string feel = Cold
                ? (_save.HeaterOn ? "студено е, но печицата грее" : Warmth >= 2 ? "студено е, но е облечен топло" : "студено му е")
                : Hot ? (Warmth >= 2 ? "горещо му е" : "топло е, но е облечен леко") : "приятно му е";
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
        _nextWarmthLine = now + 40 * 60 + _rng.Next(40 * 60);
        if (IsQuiet || InFocus || IsResting || Cooks || _state is BearState.Drag or BearState.Falling) return;
        TalkAboutWeather();
    }

    /// <summary>Казва нещо за студа/топлото (най-много веднъж на 40–80 мин).</summary>
    private void TalkAboutWeather()
    {
        if (Cold)
        {
            if (!Kitchen.Owns(_save, Kitchen.Heater))
                Say("Брр, студено е… Дали можем да си вземем печица? (магазин → Мебели)", 6);
            else if (!_save.HeaterOn)
            {
                Say("Студено ми е… Ще ми включиш ли печицата? (цъкни ме)", 8);
                _heaterAskUntil = Now + 120;
            }
            else if (Warmth < 2)
                Say(Lines.Pick(new[] { "Студено ми е… Облечи ми пуловер или шал, моля.", "Брр! Един пухкав пуловер би бил чудесен.", "*трепери* Шапка? Шал? Нещо топло?" }, _save.OwnerName), 6);
            else
                Say(Lines.Pick(new[] { "Топличко ми е, благодаря!", "*сгушва се* Обичам печицата и пуловера си.", "Навън е студено, а на мен ми е хубаво." }, _save.OwnerName), 4);
            return;
        }
        if (Hot)
        {
            if (_save.HeaterOn)
            {
                SetHeater(false);
                Say("Уф, горещо е! Изключих печицата.", 4);
            }
            else if (Warmth >= 2)
                Say(Lines.Pick(new[] { "Горещо ми е! Свали ми пуловера, моля.", "Уф, жега! Тениска би била по-добре.", "*вее си* Много съм облечен за тази жега." }, _save.OwnerName), 6);
            else
                Say(Lines.Pick(new[] { "Хубаво ми е така, по-леко.", "Лято! *протяга се на слънце*" }, _save.OwnerName), 4);
        }
    }

    /// <summary>Клик скоро след „Ще ми включиш ли печицата?“.</summary>
    private bool TakeHeaterQuestion()
    {
        if (Now > _heaterAskUntil) return false;
        _heaterAskUntil = 0;
        Ask(new ChatQuestion("Ще ми включиш ли печицата?",
            new ChatOption("🔥 Включи я", p => { p.SetHeater(true); p.Say("Ураа! Топличко! *грее лапички*", 4); }),
            new ChatOption("Не сега", p => p.Say("Добре… *сгушва се*", 3))));
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
            _heaterWindow = new CookerWindow();
            _heaterWindow.SetTime(null);
            _heaterWindow.Clicked += () => Ask(new ChatQuestion("Да изключа ли печицата?",
                new ChatOption("Изключи я", p => p.SetHeater(false)),
                new ChatOption("Не, нека грее", p => p.Say("*грее лапички* Мм.", 3))));
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
            Announce(Lines.Pick(new[] { "*грее лапички* Обичам печицата.", "Топличко… *мърка*", "Печицата е най-хубавото нещо зимата." }, _save.OwnerName), 4);
        });
    }
}
