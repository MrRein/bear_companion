using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Подаръци: веднъж на ден (а някой ден нищо) мечокът излиза от екрана и се
// връща с подарък в лапите (walk_gift): „Имам нещо за теб!“, вдига го над главата
// (gift) и после седи и го държи (gift_hold), докато Тут не го цъкне. Ако чака
// повече от 30 мин, го оставя до себе си. Подаръците са в различни цветове.
// Вътре има дреха, тапет, скин на менюто, страница или лешници.
public sealed partial class PetWindow
{
    private const double GiftChance = 0.7;          // в 7 от 10 дни има подарък
    private const double GiftReminder = 15 * 60;    // напомня на 15 мин (през Mutter)

    private enum GiftRun { None, Leaving, Away, Returning }

    private GiftRun _giftRun;
    private double _giftDeadline;      // ако нещо го прекъсне, подаръкът пак идва
    private double _nextGiftReminder;
    private GiftWindow? _giftWindow;
    private readonly Image _carry = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private bool GiftAway => _giftRun == GiftRun.Away;
    private bool CarriesGift => _giftRun == GiftRun.Returning || _giftHold != GiftHold.None;

    // Подава подаръка: първо над главата, после седи с него в скута.
    private enum GiftHold { None, Overhead, Lap }

    private GiftHold _giftHold;
    private const double GiftHoldMax = 30 * 60;   // след толкова седене го оставя до себе си

    private string GiftArt(bool open = false) => "gift" + (_save.GiftColor.Length > 0 ? "_" + _save.GiftColor : "") + (open ? "_open" : "");

    private static readonly string[] GiftColors = { "", "pink", "green", "yellow", "purple", "red" };

    /// <summary>Нов ден: решава дали днес ще има подарък и кога (след 20 мин до 3 ч).</summary>
    private void PlanGift()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_save.GiftDay == today) return;
        _save.GiftDay = today;
        _save.GiftAt = _rng.NextDouble() < GiftChance
            ? DateTime.Now.AddMinutes(20 + _rng.Next(160))
            : DateTime.MinValue;
        Persist();
    }

    /// <summary>Време е и е свободен: тръгва за подаръка (вика се от UpdateIdle).</summary>
    private bool TryStartGift()
    {
        if (_giftRun != GiftRun.None || _save.GiftPending) return false;
        if (_save.GiftAt == DateTime.MinValue || DateTime.Now < _save.GiftAt) return false;
        if (InFocus || IsQuiet || WaitingForFood || IsAsleep || Cooks || OnWalk) return false;
        StartGiftRun();
        return true;
    }

    /// <summary>Излиза през по-близкия ръб, чака малко навън и се връща с подаръка.</summary>
    private void StartGiftRun()
    {
        if (_inScene) LeaveScene();
        if (_onTrampoline) EndBounce(null);
        var area = Area;
        int dir = _x - area.Left < area.Right - _x ? -1 : 1;
        // Средата му остава в екрана (иначе при два монитора ще излезе на другия):
        // наполовина навън е, а докато го няма, е скрит.
        double outside = dir < 0 ? area.Left + 1 : area.Right - 1;
        _giftRun = GiftRun.Leaving;
        _giftDeadline = Now + 90;
        _save.GiftColor = GiftColors[_rng.Next(GiftColors.Length)];
        _peeking = true;
        WalkTo(outside, () =>
        {
            _giftRun = GiftRun.Away;
            Play("idle", length: 5 + _rng.Next(6), after: () =>
            {
                _giftRun = GiftRun.Returning;
                _peeking = true;
                var a = Area;
                // Носи го в лапите (walk_gift) и спира малко навътре от ръба.
                double spot = dir < 0 ? a.Left + SpriteWidth * 2.5 : a.Right - SpriteWidth * 2.5;
                WalkTo(spot, PresentGift);
                if (_lib.Has("walk_gift")) _anim = "walk_gift";
            });
        });
    }

    /// <summary>Върна се: „Имам нещо за теб!“, вдига подаръка над главата, после седи и го държи.</summary>
    private void PresentGift()
    {
        _giftRun = GiftRun.None;
        _peeking = false;
        _save.GiftPending = true;
        _save.GiftAt = DateTime.MinValue;
        Persist();
        _facingLeft = _x > Area.Left + Area.Width / 2;
        Say(R.Pick("gift.present", _save.OwnerName), 5);
        Emote("heart");
        _giftHold = GiftHold.Overhead;
        Play(_lib.Has("gift") ? "gift" : "happy", length: 4, after: HoldGift);
        _nextGiftReminder = Now + GiftReminder;
    }

    /// <summary>Седи с подаръка в скута и чака Тут да го цъкне (най-много 30 мин).</summary>
    private void HoldGift()
    {
        if (!_save.GiftPending) return;
        _giftHold = GiftHold.Lap;
        Play(_lib.Has("gift_hold") ? "gift_hold" : "sit", length: GiftHoldMax, after: () =>
        {
            if (_giftHold == GiftHold.None) return;
            Say(R.Pick("gift.putdown", _save.OwnerName), 4);
            PutGiftDown();
        });
    }

    /// <summary>Оставя подаръка на земята до себе си (дълго чака, вдигнаха го…).</summary>
    private void PutGiftDown()
    {
        _giftHold = GiftHold.None;
        if (_save.GiftPending) ShowGift(fromBear: true);
    }

    /// <summary>Тут цъкна мечока, докато държи подаръка: отваря се в лапите му.</summary>
    private bool OpenHeldGift()
    {
        if (_giftHold == GiftHold.None || !_save.GiftPending) return false;
        _giftHold = GiftHold.None;
        ShowGift(fromBear: true, open: true);
        return true;
    }

    /// <summary>Оставя подаръка (за стари записи и Developer: направо на земята до него).</summary>
    private void DropGift()
    {
        _giftRun = GiftRun.None;
        _giftHold = GiftHold.None;
        _peeking = false;
        _save.GiftPending = true;
        _save.GiftAt = DateTime.MinValue;
        Persist();
        ShowGift(fromBear: true);
        Say(Lines.Pick(Lines.GiftBrought, _save.OwnerName), 5);
        if (CanAnimateFreely) Play("happy");
        _nextGiftReminder = Now + GiftReminder;
    }

    private void ShowGift(bool fromBear, bool open = false)
    {
        if (_giftWindow is { IsVisible: true }) return;
        var area = Area;
        double scale = PixelSize * 1.5;
        _giftWindow = new GiftWindow(this, _lib.GetProp(GiftArt(), 16, 16, 0).Image, _lib.GetProp(GiftArt(open: true), 16, 16, 0).Image, scale);
        _giftWindow.Closed += (_, _) => _giftWindow = null;
        // На земята до мечока (от страната с повече място).
        double side = _x > area.Left + area.Width / 2 ? -1 : 1;
        var spot = new Point(Math.Clamp(_x + side * SpriteWidth, area.Left + 40, area.Right - 40), area.Bottom - 16 * scale / 2);
        if (open)
        {
            // Отваря се направо в лапите му: над главата.
            var head = new Point(_x, _bearRect.Top - 8 * scale);
            _giftWindow.FlyIn(head, head);
            OpenGift(_giftWindow);
            return;
        }
        _giftWindow.FlyIn(fromBear ? new Point(_x, _bearRect.Top + _bearRect.Height / 2) : spot, spot);
    }

    /// <summary>Тут цъкна подаръка: какво има вътре.</summary>
    internal void OpenGift(GiftWindow window)
    {
        _save.GiftPending = false;
        // Дрехи, аксесоари и тапети – само ако има гардероб.
        bool wardrobe = DailyOffer.HasWardrobe(_save);
        var clothes = Clothes.All.Where(g => wardrobe && !g.Hidden && !Clothes.Owns(_save, g)).Select(g => (object)g);
        var backdrops = Backdrops.All.Where(b => wardrobe && !Backdrops.Owns(_save, b)).Select(b => (object)b);
        var themes = UiThemes.All.Where(t => wardrobe && !UiThemes.Owns(_save, t)).Select(t => (object)t);
        var prizes = clothes.Concat(backdrops).Concat(themes).ToList();
        // Понякога (по-често, ако няма какво друго) – страница от готварската книга.
        if (Recipes.Next(_save) != null && _rng.Next(100) < (prizes.Count == 0 ? 60 : 25))
        {
            var before = Recipes.Next(_save)!;
            FindRecipePage();
            int need = Recipes.PagesFor(before);
            bool learned = Kitchen.HasRecipe(before, _save);
            window.Reveal(_lib.GetProp("shop_cookbook", 16, 16, 0).Image, 5, "Страница от готварска книга!",
                learned ? $"Научи {before.Name}!" : $"Следваща рецепта: {_save.RecipePages}/{need} страници.");
            if (!learned) Say(Lines.Pick(Lines.GiftOpened, _save.OwnerName), 4);
        }
        else if (prizes.Count == 0)
        {
            int nuts = 5 + _rng.Next(11);
            _save.Hazelnuts += nuts;
            window.Reveal(_lib.GetProp("nut", 5, 6, 0).Image, 8, $"{nuts} лешника!", wardrobe ? "Имаш вече всичко, затова лешници." : "С гардероб тук може да има и дрехи.");
            Say($"Изненада! {nuts} лешника 🌰", 4);
        }
        else
        {
            switch (prizes[_rng.Next(prizes.Count)])
            {
                case Garment g:
                    _save.OwnedClothes.Add(g.Id);
                    var art = GarmentPreview(g);
                    window.Reveal(art, Math.Clamp(80 / Math.Max(art.PixelWidth, art.PixelHeight), 3, 8), g.Name, "Отиде в гардероба.");
                    Say(Lines.Pick(Lines.GiftOpened, _save.OwnerName), 4);
                    break;
                case UiTheme t:
                    _save.Owned.Add(t.Key);
                    window.Reveal(UiThemes.Preview(t), 4, $"Меню: {t.Name}", "Сложи го от гардероба → Меню.");
                    Say(Lines.Pick(Lines.GiftOpened, _save.OwnerName), 4);
                    break;
                case Backdrop b:
                    _save.Owned.Add("bg_" + b.Id);
                    window.Reveal(BackdropImage(b), 2, $"Тапет: {b.Name}", "Сложи го от гардероба (Тапети) или с клик на портрета.");
                    Say(Lines.Pick(Lines.GiftOpened, _save.OwnerName), 4);
                    break;
            }
        }
        if (CanAnimateFreely) Play("dance", length: 2.5);
        Persist();
        NotifyCare();
    }

    /// <summary>Всеки кадър: ден, напомняне, подаръкът в ръцете му, прекъсване.</summary>
    private void UpdateGifts(double now)
    {
        PlanGift();
        if (_save.GiftPending && _giftWindow == null && _giftRun == GiftRun.None && _giftHold == GiftHold.None) ShowGift(fromBear: false);
        // Държи подаръка, а нещо го прекъсна (вдигнаха го, заспа, пуснаха работа): оставя го.
        if (_giftHold != GiftHold.None && _state != BearState.Busy) PutGiftDown();
        if (_save.GiftPending && now > _nextGiftReminder && _nextGiftReminder > 0)
        {
            _nextGiftReminder = now + GiftReminder;
            Mutter(Lines.Pick(Lines.GiftWaiting, _save.OwnerName), 5);
        }
        if (_nextGiftReminder == 0) _nextGiftReminder = now + GiftReminder;

        // Вдигнаха го, заспа… подаръкът пак идва, направо в средата.
        bool onTrack = _giftRun switch
        {
            GiftRun.Leaving or GiftRun.Returning => _state == BearState.Walk,
            GiftRun.Away => _state == BearState.Busy,
            _ => true,
        };
        if (_giftRun != GiftRun.None && (!onTrack || now > _giftDeadline))
        {
            var area = Area;
            _x = Math.Clamp(_x, area.Left + HalfWidth, area.Right - HalfWidth);
            DropGift();
        }
    }

    /// <summary>Подаръкът в лапите му, докато се връща.</summary>
    private Rect CarryRect(Rect bear)
    {
        bool on = CarriesGift;
        _carry.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return Rect.Empty;
        var img = _lib.GetProp(GiftArt(), 16, 16, 0).Image;
        // С нарисуваните анимации подаръкът е в рисунката; иначе – картинка в лапите.
        if ((_anim == "walk_gift" || _anim == "gift" || _anim == "gift_hold") && _lib.Has(_anim))
        {
            _carry.Visibility = Visibility.Collapsed;
            return Rect.Empty;
        }
        if (_carry.Source != img)
        {
            _carry.Source = img;
            RenderOptions.SetBitmapScalingMode(_carry, BitmapScalingMode.NearestNeighbor);
        }
        double w = img.PixelWidth * PixelSize * 0.75, h = img.PixelHeight * PixelSize * 0.75;
        _carry.Width = w;
        _carry.Height = h;
        double cx = bear.Left + bear.Width / 2 + (_facingLeft ? -1 : 1) * bear.Width * 0.12;
        if (_giftHold == GiftHold.Overhead)   // вдигнат над главата
            return new Rect(bear.Left + bear.Width / 2 - w / 2, bear.Top - h * 0.6 - Math.Abs(Math.Sin(Now * 5)) * PixelSize * 2, w, h);
        if (_giftHold == GiftHold.Lap)        // в скута
            return new Rect(bear.Left + bear.Width / 2 - w / 2, bear.Top + bear.Height * 0.62 - h / 2, w, h);
        return new Rect(cx - w / 2, bear.Top + bear.Height * 0.55 - h / 2, w, h);
    }

    // ── Developer ──

    internal void DevGiftNow() => StartGiftRun();

    internal void DevGiftDrop()
    {
        _giftRun = GiftRun.None;
        DropGift();
    }

    internal void DevGiftNewDay()
    {
        _save.GiftDay = "";
        _save.GiftPending = false;
        _giftWindow?.Close();
        PlanGift();
        Say(_save.GiftAt == DateTime.MinValue ? "Днес няма подарък (тест)." : $"Подарък в {_save.GiftAt:HH:mm}.", 3);
    }
}

// SOS: мечокът дава нещо за мотивация от листа в GitHub (motivation.md).
public sealed partial class PetWindow
{
    private List<Motivation> _motivations = new();
    private readonly List<Motivation> _motivationBag = new();
    private Motivation? _lastMotivation;

    public void Sos()
    {
        if (_motivations.Count == 0) _motivations = Motivations.Load();
        // Обновява листа на заден план; следващият SOS ще го ползва.
        _ = RefreshMotivations();

        // „Торба“: всичко се показва веднъж, преди да почне пак.
        if (_motivationBag.Count == 0)
            _motivationBag.AddRange(_motivations.OrderBy(_ => _rng.Next()).Where(m => m != _lastMotivation || _motivations.Count == 1));
        var m = _motivationBag[0];
        _motivationBag.RemoveAt(0);
        _lastMotivation = m;

        string intro = Lines.Pick(Lines.SosIntro, _save.OwnerName);
        var more = new ChatOption("Друго", p => p.Sos());
        var thanks = new ChatOption("Благодаря, Мечо!", p => p.Cheer(Lines.Pick(Lines.SosThanks, p.Save.OwnerName)));
        if (m.Link != null)
        {
            Ask(new ChatQuestion(intro,
                new ChatOption(m.Name, p => p.OpenLink(m.Link)),
                more, thanks));
        }
        else
        {
            Ask(new ChatQuestion($"{intro}\n\n„{m.Name}“", more, thanks));
        }
        if (CanAnimateFreely && _state != BearState.Busy) Play("happy");
    }

    private async System.Threading.Tasks.Task RefreshMotivations()
    {
        if (await Motivations.RefreshAsync())
        {
            _motivations = Motivations.Load();
            _motivationBag.Clear();
        }
    }

    public void OpenLink(string link)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link) { UseShellExecute = true });
            Cheer(Lines.Pick(Lines.SosOpened, _save.OwnerName));
        }
        catch (Exception)
        {
            Say("Не успях да го отворя…", 3);
        }
    }
}
