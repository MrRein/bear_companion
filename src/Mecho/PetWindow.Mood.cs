using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// Как се чувства (за „За Мечо“): едно състояние (горд, доволен, самотен, скучае…) и
/// нуждите (болен, изморен, гладен, премръзнал – по три степени) в едно изречение, и
/// ниво 1–5 за личицето (червено → зелено).
/// </summary>
public sealed partial class PetWindow
{
    private DateTime _proudUntil, _contentUntil, _stuffedUntil, _restedUntil, _sleepyUntil;
    private DateTime _lastTouch = DateTime.Now, _lastBusy = DateTime.Now;

    private void NoteProud() => _proudUntil = DateTime.Now.AddHours(2);
    private void NoteContent() => _contentUntil = DateTime.Now.AddMinutes(90);
    private void NoteTouched() => _lastTouch = DateTime.Now;

    /// <summary>Вика се всеки кадър: прави ли нещо (за „Скучае“).</summary>
    private void NoteBusy()
    {
        if (IsAsleep || OnWalk || InFocus || CurrentFun != null || WorksRandomly || RelaxesRandomly || MenuIsOpen || ChatIsOpen)
            _lastBusy = DateTime.Now;
    }

    /// <summary>Хапна: преял ли е (току-що догоре) и доволен ли е (желаното или любимото).</summary>
    private void NoteAte(Food f)
    {
        if (_save.Fullness >= 97) _stuffedUntil = DateTime.Now.AddMinutes(60);
        if (_save.WishList.Contains(f.Id) || _save.FavFood == f.Id) NoteContent();
    }

    /// <summary>Събуди се: сутрин – сънен; след дрямка – отпочинал.</summary>
    private void NoteWoke(bool morning)
    {
        if (morning) _sleepyUntil = DateTime.Now.AddMinutes(45);
        else if (_save.Energy >= 80) _restedUntil = DateTime.Now.AddMinutes(90);
    }

    /// <summary>Едно изречение и ниво 1 (много зле) – 5 (щастлив).</summary>
    public (int Level, string Text) MoodNow
    {
        get
        {
            var now = DateTime.Now;
            double e = _save.Energy, f = _save.Fullness;
            // Нуждите: (дума, тежест 1–3).
            var needs = new List<(string Word, int Weight, string Kind)>();
            if (IsSick) needs.Add(("болен", 3, "sick"));
            if (!IsAsleep)
            {
                if (e < 15) needs.Add(("много изморен", 3, "tired"));
                else if (e < 30) needs.Add(("изморен", 2, "tired"));
                else if (e < 50) needs.Add(("леко изморен", 1, "tired"));
            }
            if (f < 20) needs.Add(("много гладен", 3, "hungry"));
            else if (f < 35) needs.Add(("гладен", 2, "hungry"));
            else if (f < 50) needs.Add(("леко гладен", 1, "hungry"));
            if (NeedsWarmerClothes) needs.Add(("премръзнал", 2, "cold"));

            // Състоянието: (дума, вид: 1 хубаво, 0 неутрално, -1 не толкова, -2 зле).
            (string Word, int Tone)? state =
                IsAsleep ? ("Спи", 0)
                : Overloaded ? ("Претоварен", -2)
                : InFocus ? (InFlow ? ("В ритъм", 1) : ("Съсредоточен", 0))
                : now < _proudUntil ? ("Горд", 1)
                : _save.GiftPending ? ("Развълнуван", 1)
                : now < _contentUntil ? ("Доволен", 1)
                : now < _stuffedUntil ? ("Преял", 0)
                : now < _restedUntil ? ("Отпочинал", 1)
                : now < _sleepyUntil ? ("Сънен", 0)
                : WantsPetting || CurrentDesire != null ? ("Иска внимание", 0)
                : now - _lastTouch > TimeSpan.FromHours(3) && now - _launchedAt > TimeSpan.FromHours(3) ? ("Самотен", -1)
                : now - _lastBusy > TimeSpan.FromMinutes(40) ? ("Скучае", -1)
                : CurrentFun is { } fun && !Scenes.Work.Contains(fun) || RelaxesRandomly ? ("Спокоен", 0)
                : null;

            // Повтарящото се не се казва два пъти: сънен/спи – без умората, преял – без глада.
            if (state is { Word: "Сънен" or "Спи" or "Отпочинал" }) needs.RemoveAll(n => n.Kind == "tired");
            if (state is { Word: "Преял" or "Доволен" }) needs.RemoveAll(n => n.Kind == "hungry");

            string list = needs.Count switch
            {
                0 => "",
                1 => needs[0].Word,
                _ => string.Join(", ", needs.Take(needs.Count - 1).Select(n => n.Word)) + " и " + needs[^1].Word,
            };

            string text;
            int tone;
            if (state is not { } st)
            {
                if (needs.Count == 0)
                {
                    bool happy = f >= 70 && e >= 60;
                    return (happy ? 5 : 4, happy ? "Щастлив" : "Добре");
                }
                text = Capital(list);
                tone = 0;
            }
            else
            {
                tone = st.Tone;
                text = needs.Count == 0 ? st.Word
                    : st.Word == "Спи" ? "Спи, но е " + list
                    : st.Tone > 0 || st.Word == "Съсредоточен" ? st.Word + ", но " + list
                    : st.Word + " и " + list;
            }

            int level = tone switch { 1 => 5, 0 => 4, -1 => 3, _ => 2 };
            if (needs.Count > 0)
            {
                int worst = needs.Max(n => n.Weight);
                int sum = needs.Sum(n => n.Weight);
                int cap = sum >= 5 ? 1 : 4 - worst;
                level = Math.Min(level, cap);
            }
            return (Math.Clamp(level, 1, 5), text);
        }
    }

}
