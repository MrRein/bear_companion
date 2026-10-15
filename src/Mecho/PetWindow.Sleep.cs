using System;

namespace Mecho;

// Сън: винаги за определено време. Тут избира колко (20 мин, 1 ч, 2 ч или свое
// число); вечер (от 20 ч.) – „Лягай за деня“: спи до сутринта и става между 8 и
// 10 ч. Цъкнеш ли го, докато спи, казва след колко ще стане. Събудиш ли го
// по-рано сутрин (пуснала си компютъра рано) – не се сърди.
public sealed partial class PetWindow
{
    /// <summary>Вечер е: може да легне за деня.</summary>
    public static bool LateForBed => DateTime.Now.Hour >= 20 || DateTime.Now.Hour < 5;

    /// <summary>Спи до сутринта („Лягай за деня“).</summary>
    public bool SleepsForDay => IsAsleep && _save.SleepForDay;

    /// <summary>Колко още ще спи.</summary>
    public TimeSpan SleepLeft => IsAsleep && _save.NapUntil != DateTime.MinValue
        ? Max(TimeSpan.Zero, _save.NapUntil - DateTime.Now)
        : TimeSpan.Zero;

    /// <summary>„след 1 ч 20 мин“ или „в 8:40“ (за сутринта).</summary>
    public string WakeText
    {
        get
        {
            if (!IsAsleep || _save.NapUntil == DateTime.MinValue) return "";
            if (_save.SleepForDay) return $"в {_save.NapUntil:H:mm}";
            var left = SleepLeft;
            if (left.TotalMinutes < 1) return "след малко";
            return left.TotalHours >= 1 ? $"след {(int)left.TotalHours} ч {left.Minutes} мин" : $"след {Math.Ceiling(left.TotalMinutes)} мин";
        }
    }

    /// <summary>Приспи (менюто, иконката до часовника): вечер – за деня, иначе за час.</summary>
    public void GoToSleep()
    {
        if (LateForBed) SleepForDay();
        else Sleep(60);
    }

    /// <summary>Спи толкова минути (наспива се напълно, ако времето стигне).</summary>
    public void Sleep(double minutes)
    {
        if (minutes <= 0) return;
        BeginSleep(DateTime.Now.AddMinutes(minutes), 100, forDay: false, Lines.Pick(Lines.GoingToSleep, _save.OwnerName));
    }

    /// <summary>„Лягай за деня“: спи до сутринта, става между 8 и 10 ч.</summary>
    public void SleepForDay()
    {
        var day = DateTime.Now.Hour < 5 ? DateTime.Today : DateTime.Today.AddDays(1);
        var until = day.AddHours(8).AddMinutes(_rng.Next(121));
        BeginSleep(until, 100, forDay: true, R.Pick("sleep.forday", _save.OwnerName));
        if (IsAsleep || _save.SleepForDay) RecordBed();
    }

    /// <summary>Дрямка (когато сам пита): става сам, по-бодър, но не догоре.</summary>
    public void TakeNap(int minutes) =>
        BeginSleep(DateTime.Now.AddMinutes(minutes), Math.Min(100, _save.Energy + minutes * 1.8 + 5), forDay: false,
            R.Pick("nap.going", _save.OwnerName).Replace("{число}", minutes.ToString()));

    private void BeginSleep(DateTime until, double cap, bool forDay, string line)
    {
        if (IsAsleep || BusyEating()) return;
        // Не заспива, докато нещо се готви: ще загори!
        if (_cook != null || _pendingCook != null)
        {
            Say(Lines.Pick(Lines.NoSleepCooking, _save.OwnerName), 4);
            return;
        }
        if (_save.Energy >= 95 && !forDay)
        {
            Say(R.Any("sleep.nottired"), 3);
            return;
        }
        if (_onTrampoline) EndBounce(null);
        if (_inScene) LeaveScene();
        _save.StayPut = false;
        _save.NapUntil = until;
        _save.NapCap = cap;
        _save.SleepForDay = forDay;
        _save.SleepingByChoice = true;
        if (forDay) _save.WentToBed = DateTime.Now;
        Say(line, 4);
        Play("yawn", then: "sleep");
        NotifyCare();
        Persist();
    }

    /// <summary>Тут го събуди.</summary>
    public void WakeUp()
    {
        bool wasDay = _save.SleepForDay;
        bool early = wasDay && DateTime.Now.Hour is >= 5 and < 12;
        _save.SleepingByChoice = false;
        _save.NapUntil = DateTime.MinValue;
        _save.SleepForDay = false;
        if (IsAsleep || _afterBusy == "sleep")
        {
            if (early) TodayRec.WokeEarly = true;
            else if (!wasDay) TodayRec.NapWoken = true;
            // Сутрин по-рано от времето си: не се сърди.
            Say(early ? R.Pick("sleep.wokeearly", _save.OwnerName) : Lines.Pick(Lines.WokeUp, _save.OwnerName), 4);
            Play("happy");
        }
        NotifyCare();
        Persist();
    }

    /// <summary>Вика се от UpdateSleep: времето за сън изтече ли – става.</summary>
    private bool NapOver()
    {
        // Стар запис (без час за ставане): вечер – до сутринта, иначе още половин час.
        if (_save.NapUntil == DateTime.MinValue)
        {
            if (_sleepIsAway) return false;
            if (LateForBed)
            {
                var morning = DateTime.Now.Hour < 5 ? DateTime.Today : DateTime.Today.AddDays(1);
                _save.NapUntil = morning.AddHours(8).AddMinutes(_rng.Next(121));
                _save.SleepForDay = true;
            }
            else _save.NapUntil = DateTime.Now.AddMinutes(30);
            _save.NapCap = 100;
            return false;
        }
        if (DateTime.Now < _save.NapUntil) return false;
        bool forDay = _save.SleepForDay;
        _save.NapUntil = DateTime.MinValue;
        _save.SleepForDay = false;
        _save.SleepingByChoice = false;
        Say(forDay ? R.Pick("sleep.morning", _save.OwnerName) : R.Pick("nap.woke", _save.OwnerName), 5);
        Play("happy");
        NotifyCare();
        Persist();
        return true;
    }
}
