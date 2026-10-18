using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Подаръци: веднъж на ден (а някой ден нищо) мечокът излиза от екрана и се
// връща с подарък пред себе си (hold_walk): „Имам нещо за теб!“. После стои и го
// държи (hold), докато Тут не го цъкне:
//  • цъкнеш мечока – „Заповядай!“, пуска го, подаръкът долита в средата на
//    екрана и се отваря сам;
//  • дълго не го вземеш (или нещо го прекъсне – вдигнат, заспи, работа) – оставя
//    го в средата на екрана, където чака да го цъкнеш;
//  • като се отвори – мечокът коментира какво е и (дреха, тапет, меню) пита дали
//    да го сложи веднага.
// Подаръците са в различни цветове. Вътре има дреха, тапет, скин на менюто,
// страница от готварската книга или лешници.
public sealed partial class PetWindow
{
    private const double GiftReminder = 15 * 60;    // напомня на 15 мин (през Mutter)
    private const double GiftHoldMax = 10 * 60;     // толкова го държи, после го оставя в средата

    private enum GiftRun { None, Leaving, Away, Returning, Holding }

    private GiftRun _giftRun;
    private double _giftDeadline;      // ако нещо го прекъсне, подаръкът пак идва
    private double _nextGiftReminder;
    private GiftWindow? _giftWindow;
    private readonly Image _carry = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private bool GiftAway => _giftRun == GiftRun.Away;

    /// <summary>Подаръкът е в лапите му (носи го или го държи).</summary>
    private bool CarriesGift => _giftRun is GiftRun.Returning or GiftRun.Holding;

    private string GiftSuffix => _save.GiftColor.Length > 0 ? "_" + _save.GiftColor : "";

    private string GiftArt(bool open = false) => "gift" + GiftSuffix + (open ? "_open" : "");

    /// <summary>Носи го пред себе си: hold_walk (стари рисунки: walk_gift в цвета на подаръка).</summary>
    private string GiftWalk => _lib.Has("hold_walk") ? "hold_walk" : _lib.Has("walk_gift" + GiftSuffix) ? "walk_gift" + GiftSuffix : "walk_gift";

    private string GiftHoldAnim => _lib.Has("hold") ? "hold" : "idle";

    private static readonly string[] GiftColors = { "", "pink", "green", "yellow", "purple", "red" };

    /// <summary>Нов ден: решава дали днес ще има подарък и кога (след 20 мин до 3 ч).</summary>
    private void PlanGift()
    {
        // Решава ядрото (GiftsModule: шанс × Mods.GiftChance – празниците го вдигат).
        string day = _save.GiftDay;
        CoreEngine.Bus.Send(new Mecho.Core.PlanGiftToday());
        if (_save.GiftDay != day) Persist();
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
                // Носи го пред себе си и спира малко навътре от ръба.
                double spot = dir < 0 ? a.Left + SpriteWidth * 2.5 : a.Right - SpriteWidth * 2.5;
                WalkTo(spot, PresentGift);
                if (_lib.Has(GiftWalk)) _anim = GiftWalk;
            });
        });
    }

    /// <summary>Върна се: „Имам нещо за теб!“ и стои, държейки подаръка пред себе си.</summary>
    private void PresentGift()
    {
        _peeking = false;
        CoreEngine.Bus.Send(new Mecho.Core.GiftPresented(_save.GiftColor));
        Persist();
        Say(R.Pick("gift.present", _save.OwnerName), 5);
        Emote("heart");
        HoldGift();
        _nextGiftReminder = Now + GiftReminder;
    }

    /// <summary>Стои с подаръка в лапите (hold) и чака Тут да го цъкне.</summary>
    private void HoldGift()
    {
        if (!_save.GiftPending) return;
        _giftRun = GiftRun.Holding;
        Play(GiftHoldAnim, length: GiftHoldMax, after: () =>
        {
            if (_giftRun != GiftRun.Holding) return;
            Say(R.Pick("gift.putdown", _save.OwnerName), 4);
            PutGiftDown();
        });
    }

    /// <summary>Оставя подаръка в средата на екрана (дълго чака, вдигнаха го, заспа…) – чака да го цъкнеш.</summary>
    private void PutGiftDown()
    {
        bool holding = _giftRun == GiftRun.Holding && _state == BearState.Busy && _anim == GiftHoldAnim;
        _giftRun = GiftRun.None;
        if (holding) SetIdle();
        _peeking = false;
        if (_save.GiftPending) ShowGift(fromBear: true);
    }

    /// <summary>Тут цъкна мечока, докато държи подаръка: пуска го, той долита в средата и се отваря.</summary>
    private bool OpenHeldGift()
    {
        if (_giftRun != GiftRun.Holding || !_save.GiftPending) return false;
        _giftRun = GiftRun.None;
        SetIdle();   // лапите са свободни
        Say(R.Pick("gift.giving", _save.OwnerName), 3);
        ShowGift(fromBear: true, open: true);
        React("happy");
        return true;
    }

    /// <summary>Оставя подаръка (стари записи, прекъснат път, Developer): направо в средата.</summary>
    private void DropGift()
    {
        _giftRun = GiftRun.None;
        _peeking = false;
        _save.GiftPending = true;
        _save.GiftAt = DateTime.MinValue;
        Persist();
        ShowGift(fromBear: true);
        Say(Lines.Pick(Lines.GiftBrought, _save.OwnerName), 5);
        React("happy");
        _nextGiftReminder = Now + GiftReminder;
    }

    /// <summary>Средата на работната площ (там чака и се отваря подаръкът).</summary>
    private Point GiftCenter
    {
        get
        {
            var a = Area;
            return new Point(a.Left + a.Width / 2, a.Top + a.Height / 2);
        }
    }

    /// <summary>Подаръкът долита от лапите му (или се появява) в средата; open – отваря се щом кацне.</summary>
    private void ShowGift(bool fromBear, bool open = false)
    {
        if (_giftWindow is { IsVisible: true })
        {
            if (open) _giftWindow.OpenWhenLanded();
            return;
        }
        double scale = PixelSize * 1.5;
        _giftWindow = new GiftWindow(this, _lib.GetProp(GiftArt(), 16, 16, 0).Image, _lib.GetProp(GiftArt(open: true), 16, 16, 0).Image, scale);
        Companion(_giftWindow);
        _giftWindow.Closed += (_, _) => _giftWindow = null;
        var center = GiftCenter;
        var paws = new Point(_x, _bearRect.Top + _bearRect.Height * 0.66);
        _giftWindow.FlyIn(fromBear && IsVisible ? paws : center, center);
        if (open) _giftWindow.OpenWhenLanded();
    }

    /// <summary>Тут цъкна подаръка: какво има вътре.</summary>
    internal void OpenGift(GiftWindow window)
    {
        if (!_save.GiftPending)
        {
            window.Close();
            return;
        }
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
            Later(1.2, () => Say(R.Text(learned ? "gift.got.recipe" : "gift.got.page", _save.OwnerName, ("храна", before.Name.ToLowerInvariant())), 5));
        }
        else if (prizes.Count == 0)
        {
            int nuts = 5 + _rng.Next(11);
            _save.Hazelnuts += nuts;
            window.Reveal(_lib.GetProp("nut", 5, 6, 0).Image, 8, $"{nuts} лешника!", wardrobe ? "Имаш вече всичко, затова лешници." : "С гардероб тук може да има и дрехи.");
            Later(1.2, () => Say(R.Text("gift.got.nuts", _save.OwnerName, ("число", nuts.ToString())), 4));
        }
        else
        {
            switch (prizes[_rng.Next(prizes.Count)])
            {
                case Garment g:
                    _save.OwnedClothes.Add(g.Id);
                    var art = GarmentPreview(g);
                    window.Reveal(art, Math.Clamp(80 / Math.Max(art.PixelWidth, art.PixelHeight), 3, 8), g.Name, "Отиде в гардероба.");
                    GiftReaction("gift.got.clothes", g.Name, "Да го облека ли сега?", p => p.WearGift(g));
                    break;
                case UiTheme t:
                    _save.Owned.Add(t.Key);
                    window.Reveal(UiThemes.Preview(t), 4, $"Меню: {t.Name}", "Сложи го от гардероба → Меню.");
                    GiftReaction("gift.got.theme", t.Name, "Да сменя ли менюто с него?", p => p.UseUiTheme(t));
                    break;
                case Backdrop b:
                    _save.Owned.Add("bg_" + b.Id);
                    window.Reveal(BackdropImage(b), 2, $"Тапет: {b.Name}", "Сложи го от гардероба (Тапети) или с клик на портрета.");
                    GiftReaction("gift.got.backdrop", b.Name, "Да го сложа ли за фон?", p => p.UseBackdrop(b));
                    break;
            }
        }
        React("dance", 2.5);
        Persist();
        NotifyCare();
    }

    /// <summary>Всеки кадър: ден, напомняне, подаръкът в ръцете му, прекъсване.</summary>
    private void UpdateGifts(double now)
    {
        PlanGift();
        if (_save.GiftPending && _giftWindow == null && _giftRun == GiftRun.None) ShowGift(fromBear: false);
        // Държи подаръка, а нещо го прекъсна (вдигнаха го, заспа, пуснаха работа): оставя го в средата.
        if (_giftRun == GiftRun.Holding && (_state != BearState.Busy || _anim != GiftHoldAnim))
        {
            if (DragTalk()) Say(R.Pick("gift.dropped", _save.OwnerName), 3);
            PutGiftDown();
        }
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
        if (_giftRun is GiftRun.Leaving or GiftRun.Away or GiftRun.Returning && (!onTrack || now > _giftDeadline))
        {
            var area = Area;
            _x = Math.Clamp(_x, area.Left + HalfWidth, area.Right - HalfWidth);
            DropGift();
        }
    }

    /// <summary>Подаръкът в лапите му (носи го или го държи): пред корема, на височината на лапите.</summary>
    private Rect CarryRect(Rect bear)
    {
        bool on = CarriesGift && _state is BearState.Walk or BearState.Busy;
        // Старата рисунка walk_gift е с подаръка в самата нея.
        if (on && _anim.StartsWith("walk_gift") && _lib.Has(_anim)) on = false;
        _carry.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return Rect.Empty;
        var img = _lib.GetProp(GiftArt(), 16, 16, 0).Image;
        if (_carry.Source != img)
        {
            _carry.Source = img;
            RenderOptions.SetBitmapScalingMode(_carry, BitmapScalingMode.NearestNeighbor);
        }
        double w = img.PixelWidth * PixelSize * 0.75, h = img.PixelHeight * PixelSize * 0.75;
        _carry.Width = w;
        _carry.Height = h;
        // hold/hold_walk са с лице напред: подаръкът е в средата, малко подскача при ходене.
        double bob = _state == BearState.Walk ? Math.Abs(Math.Sin(Now * 8)) * PixelSize : 0;
        return new Rect(bear.Left + bear.Width / 2 - w / 2, bear.Top + bear.Height * 0.68 - h / 2 - bob, w, h);
    }

    /// <summary>След отварянето: коментира подаръка и (дреха, меню, тапет) пита дали да го сложи.</summary>
    private void GiftReaction(string key, string name, string question, Action<PetWindow> use)
    {
        Later(1.2, () =>
        {
            string line = R.Text(key, _save.OwnerName, ("нещо", name.ToLowerInvariant()));
            if (InFocus || IsQuiet)
            {
                Mutter(line, 4);
                return;
            }
            Ask(new ChatQuestion(line + " " + question,
                new ChatOption("✨ Да, сложи го", p => { use(p); p.Say(R.Pick("gift.used", p.Save.OwnerName), 3); }),
                new ChatOption("По-късно", p => p.Say(R.Pick("gift.later", p.Save.OwnerName), 3))));
        });
    }

    /// <summary>Облича дрехата от подаръка (както от гардероба).</summary>
    internal void WearGift(Garment g)
    {
        if (Clothes.Owns(_save, g)) Wear(g);
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
        if (_state != BearState.Busy) React("happy");
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
