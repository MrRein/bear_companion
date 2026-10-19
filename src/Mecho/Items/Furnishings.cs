using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Mecho.Core;

namespace Mecho;

// МЕБЕЛИ ЗА СЛАГАНЕ: бюро, компютър, статив, настолна игра, кошница с прежда, диван,
// постелка за йога, легло. Работят като другите предмети на земята (радио, растение):
//  • Тут ги слага от „Неща на Мечо“ – стоят, докато не ги прибере; местят се с мишката;
//  • мечокът избере ли занимание с мебел (Работи си / Почивай си / избрано от Тут):
//      сложена е → отива до нея и почва;
//      не е → намира място без други неща наоколо, слага я сам (doing), прави
//      заниманието и като свърши, тя изчезва (PlacedBy.Bear – ядрото я маха);
//  • „до мебел“ (бюро, компютър, статив, игра, прежда): сяда на обикновеното столче до нея;
//  • „върху мебел“ (диван, постелка, легло): докато е на нея, тя се рисува под мечока
//    (прозорецът ѝ се скрива), а щом стане – пак е в своя прозорец.
// Правилата „кой я е сложил, кога се маха“ са в ядрото (PlacementModule).
public sealed partial class PetWindow
{
    /// <summary>Една мебел: вид, име на прозореца, рисунка и дали се сяда/ляга върху нея.</summary>
    private sealed class Furnishing
    {
        public required string Kind { get; init; }
        public required string Title { get; init; }
        public required Func<string> Art { get; init; }
        /// <summary>Как се казва в „Да си сложа {какво}…“ (членувано).</summary>
        public string Noun { get; init; } = "";
        /// <summary>Сяда/ляга върху нея (диван, постелка, легло); иначе е до него.</summary>
        public bool Seat { get; init; }
        public int W { get; init; } = 20;
        public int H { get; init; } = 20;
        public CookerWindow? Window;
        public string Shown = "";
    }

    private Furnishing[] _furnishings = Array.Empty<Furnishing>();

    private void InitFurnishings()
    {
        _furnishings = new[]
        {
            new Furnishing { Kind = Kitchen.Desk, Noun = "бюрото", Title = "Бюро", Art = () => Furniture.CurrentArt(_save, Kitchen.Desk), W = 24, H = 22 },
            new Furnishing { Kind = Kitchen.Computer, Noun = "компютъра", Title = "Компютър", Art = () => "computer", W = 20, H = 22 },
            new Furnishing { Kind = Kitchen.Easel, Noun = "статива", Title = "Статив", Art = EaselArt, W = 18, H = 36 },
            new Furnishing { Kind = Kitchen.BoardGame, Noun = "настолната игра", Title = "Настолна игра", Art = () => "table_board", W = 20, H = 16 },
            new Furnishing { Kind = Kitchen.YarnBasket, Noun = "кошницата с преждата", Title = "Кошница с прежда", Art = () => "yarn_basket", W = 16, H = 12 },
            new Furnishing { Kind = Kitchen.Couch, Noun = "дивана", Title = "Диван", Art = () => Furniture.Current(_save, Kitchen.Couch).Prop, Seat = true, W = 48, H = 24 },
            new Furnishing { Kind = Kitchen.YogaMat, Noun = "постелката", Title = "Постелка", Art = () => "mat", Seat = true, W = 40, H = 3 },
            new Furnishing { Kind = Kitchen.Bed, Noun = "леглото", Title = "Легло", Art = () => Furniture.Current(_save, Kitchen.Bed).Prop, Seat = true, W = 46, H = 16 },
        };
    }

    /// <summary>Видовете мебели за слагане (за менюто „Неща на Мечо“), по ред.</summary>
    public static readonly string[] FurnitureKinds =
        { Kitchen.Desk, Kitchen.Computer, Kitchen.Easel, Kitchen.BoardGame, Kitchen.YarnBasket, Kitchen.Couch, Kitchen.YogaMat, Kitchen.Bed };

    private Furnishing? FurnishingOf(string? kind) => kind == null ? null : _furnishings.FirstOrDefault(f => f.Kind == kind);

    private Mecho.Core.Placement PlacementOf(string kind) => CoreEngine.Store.Get<PlacementState>().Get(kind);

    /// <summary>Сложена ли е на земята (от Тут или от мечока).</summary>
    public bool FurniturePlaced(string kind) => Kitchen.Owns(_save, kind) && PlacementOf(kind).Placed;

    /// <summary>Мебелта на кътчето (ако е купена), или null.</summary>
    private Furnishing? FurnishingFor(Scene scene) =>
        scene.Item != null && Kitchen.Owns(_save, scene.Item) ? FurnishingOf(scene.Item) : null;

    private BitmapSource FurnitureImage(Furnishing f) => _lib.GetProp(f.Art(), f.W, f.H, f.Seat ? 7 : 0).Image;

    private double FurnitureWidth(Furnishing f) => FurnitureImage(f).PixelWidth * PixelSize;

    /// <summary>Тут я сложи (или прибра) от „Неща на Мечо“. Прибирането е веднага; слагането – мечокът я носи (PlaceByHand).</summary>
    public void SetFurniture(string kind, bool placed, double x = double.NaN)
    {
        if (!Kitchen.Owns(_save, kind)) return;
        var bus = CoreEngine.Bus;
        if (placed) bus.Send(new PlaceItem(kind, PlacedBy.Player, double.IsNaN(x) ? NearBear(FurnitureWidth(FurnishingOf(kind)!)) : x));
        else bus.Send(new RemoveItem(kind));
        Persist();
        NotifyCare();
    }

    /// <summary>Котвата на мебелта: къде е и откъде се ползва (до нея – от страната на SideDir; върху нея – в средата).</summary>
    private ItemAnchor FurnitureAnchor(Furnishing f, Scene? scene)
    {
        double overlap = (f.Kind == Kitchen.Easel ? 5 : 2) * PixelSize;
        return new ItemAnchor(f.Kind, () =>
        {
            if (!FurniturePlaced(f.Kind)) return null;
            if (f.Window != null) return f.Window.Bounds;
            double x = PlacementOf(f.Kind).X;
            if (double.IsNaN(x)) return null;
            var img = FurnitureImage(f);
            double w = img.PixelWidth * PixelSize, h = img.PixelHeight * PixelSize;
            return new Rect(x - w / 2, AreaAt(x, _y - 1).Bottom - h, w, h);
        }, SpriteWidth / 2 - overlap, SideDir: f.Seat ? 0 : (scene?.SideDir is -1 ? -1 : 1), Seat: f.Seat);
    }

    /// <summary>Върху мебелта ли е сега (за дивана/постелката под него): стигнал е и не е вдигнат.</summary>
    private bool OnFurniture(Furnishing f) =>
        _placing == null && !_anchorWalk && _state != BearState.Drag && _state != BearState.Falling
        && _sceneNear is { Seat: true } a && a.Kind == f.Kind && a.Target() is { } r
        && Math.Abs(_x - (r.Left + r.Width / 2)) <= SpriteWidth * 0.2;

    /// <summary>Мебелта е под мечока (рисува се в неговия прозорец): нейният се скрива.</summary>
    private bool FurnitureUnderBear(Furnishing f) =>
        f.Kind == Kitchen.Bed ? BedOn
        : _inScene && _seatKind == f.Kind && _seatProp != null && _state == BearState.Couch;

    /// <summary>Сложена от мечока и вече не му трябва (заниманието свърши, събуди се) – ядрото я маха.</summary>
    private bool FurnitureStillNeeded(Furnishing f) =>
        f.Kind == Kitchen.Bed
            ? IsAsleep || _dragAsleep || (_save.SleepingByChoice && _save.NapUntil > DateTime.Now)
            : _inScene && _scene?.Item == f.Kind;

    /// <summary>На кадър: прозорците на сложените мебели (мести, скрива под мечока, маха ненужните).</summary>
    private void UpdateFurnishings()
    {
        foreach (var f in _furnishings)
        {
            var p = PlacementOf(f.Kind);
            if (p.Placed && p.Owner == PlacedBy.Bear && !FurnitureStillNeeded(f)) CoreEngine.Bus.Send(new ReleaseByBear(f.Kind));
            bool placed = FurniturePlaced(f.Kind);
            if (!placed)
            {
                if (f.Window != null)
                {
                    f.Window.Close();
                    f.Window = null;
                    f.Shown = "";
                }
                continue;
            }
            string art = f.Art();
            if (f.Window == null)
            {
                var img = FurnitureImage(f);
                double half = img.PixelWidth * PixelSize / 2;
                double x = double.IsNaN(PlacementOf(f.Kind).X) ? NearBear(half * 2) : PlacementOf(f.Kind).X;
                var area = AreaAt(x, _y - 1);
                x = Math.Clamp(x, area.Left + half, Math.Max(area.Left + half, area.Right - half));
                var w = new CookerWindow(f.Title);
                f.Window = w;
                Companion(w, f.Kind);   // диванът и стативът са зад мечока (Behaviour.BehindBear)
                w.SetTime(null);
                w.Hint = f.Title + ".";
                var furn = f;
                w.Clicked += () => FurnitureClicked(furn);
                w.Dropped += (cx, _) =>
                {
                    var a = AreaAt(cx, w.Top + w.ActualHeight / 2);
                    double hw = w.ActualWidth / 2;
                    double nx = Math.Clamp(cx, a.Left + hw, Math.Max(a.Left + hw, a.Right - hw));
                    w.FallTo(nx, a.Bottom);
                    CoreEngine.Bus.Send(new MoveItem(furn.Kind, nx));
                    Persist();
                    KeepBearInFront();   // преместен диван/статив: мечокът пак е пред него
                };
                w.SetAway(!IsVisible || FurnitureUnderBear(f));
                w.Place(img, PixelSize, x, area.Bottom);
                f.Shown = art;
                CoreEngine.Bus.Send(new MoveItem(f.Kind, x));
            }
            else if (art != f.Shown)
            {
                f.Shown = art;
                f.Window.SetArt(FurnitureImage(f), PixelSize);   // нов скин, друга картина на статива
            }
            f.Window.SetAway(!IsVisible || FurnitureUnderBear(f));
            f.Window.Step(0.033);
        }
    }

    /// <summary>Цъкна мебелта: да я прибере ли.</summary>
    private void FurnitureClicked(Furnishing f)
    {
        var options = new List<ChatOption>();
        if (f.Kind == Kitchen.Bed) options.Add(new ChatOption("Лягай да спиш", p => p.AskSleepTime()));
        // Всичко, което прави с нея (настолната игра: играй / работи върху нея; стативът: корица / за удоволствие).
        foreach (var scene in Scenes.All.Where(s => s.Item == f.Kind && CanDo(s)))
        {
            var sc = scene;
            options.Add(new ChatOption(sc.Name, p => p.Relax(sc)));
        }
        options.Add(new ChatOption("Прибери", p => p.SetFurniture(f.Kind, false)));
        options.Add(new ChatOption("Остави", _ => { }));
        Ask(new ChatQuestion(f.Title + ". Какво да правим?", options.ToArray()));
    }

    // ── Свободно място ──

    /// <summary>Всичко, което стои на земята сега (за да не слага едно върху друго).</summary>
    private IEnumerable<Rect> ThingsOnGround()
    {
        foreach (var w in new[] { _heaterWindow, _lampWindow, _radioWindow, _feederWindow, _potWindow, _cook?.Window })
            if (w != null) yield return w.Bounds;
        foreach (var f in _furnishings)
            if (f.Window != null) yield return f.Window.Bounds;
            else if (FurnitureUnderBear(f)) yield return new Rect(_x - FurnitureWidth(f) / 2, _y - 1, FurnitureWidth(f), 1);
    }

    /// <summary>
    /// Среда за нещо широко width на земята под мечока: най-близкото място, около което
    /// (с малко разстояние) няма други сложени неща. Няма такова – до мечока.
    /// </summary>
    private double FreeSpot(double width, double margin = -1)
    {
        var area = AreaAt(_x, _y - 1);
        if (margin < 0) margin = SpriteWidth * 0.9;   // да има място и мечокът да застане до него
        var things = ThingsOnGround().Where(r => r.Bottom > area.Bottom - SpriteHeight * 2 && r.Top < area.Bottom + 4).ToList();
        double half = width / 2;
        double lo = area.Left + half + 4, hi = area.Right - half - 4;
        if (hi < lo) return Math.Clamp(_x, area.Left, area.Right);
        bool Free(double c) => things.All(r => c + half + margin <= r.Left || c - half - margin >= r.Right);
        double step = 6 * PixelSize;
        // Първо до мечока (от двете страни), после все по-далече.
        double start = Math.Clamp(_x, lo, hi);
        for (double d = 0; d <= area.Width; d += step)
            foreach (double c in new[] { start + d, start - d })
                if (c >= lo && c <= hi && Free(c)) return c;
        return start;
    }

    /// <summary>Място до мечока (откъм средата на екрана) за нещо широко width.</summary>
    private double NearBear(double width)
    {
        var area = AreaAt(_x, _y - 1);
        int side = _x > area.Left + area.Width / 2 ? -1 : 1;
        double x = _x + side * (width / 2 + SpriteWidth * 0.6);
        double half = width / 2;
        return Math.Clamp(x, area.Left + half, Math.Max(area.Left + half, area.Right - half));
    }

    // ── Мечокът слага мебелта сам (за кътчето) ──

    private string? _placing;          // мебелта, която отива да сложи (null – няма такава)
    private double _placingX;          // къде ще е тя (средата)
    private double _placingStand;      // къде застава, докато я слага
    private double _placingAt;         // кога я слага (doing до тогава); 0 – още върви натам

    /// <summary>Кътче с мебел, която не е сложена: отива на свободно място и я слага (после е до/върху нея).</summary>
    private void BeginPlacing(Furnishing f)
    {
        double w = FurnitureWidth(f);
        if (InstantModes)
        {
            // Самопроверката не чака разходки: слага я веднага точно до/под него.
            var a = FurnitureAnchor(f, _scene);
            double at = f.Seat ? _x : _x + a.SideDir * (w / 2 + a.Gap);
            CoreEngine.Bus.Send(new PlaceItem(f.Kind, PlacedBy.Bear, at));
            _sceneNear = a;
            _anchorWalk = false;
            return;
        }
        double c = FreeSpot(w);
        int side = c >= _x ? 1 : -1;   // мебелта е от тази страна на мечока
        var area = AreaAt(c, _y - 1);
        double stand = Math.Clamp(c - side * (w / 2 + SpriteWidth * 0.35), area.Left + SpriteWidth / 2, area.Right - SpriteWidth / 2);
        if (!IsQuiet && _rng.NextDouble() < 0.4) Mutter(R.Pick("behaviour.furniture.place", _save.OwnerName).Replace("{какво}", f.Noun), 3);
        _placing = f.Kind;
        _placingX = c;
        _placingStand = stand;
        _placingAt = 0;
        _anchorWalk = false;
    }

    /// <summary>Слага мебелта: върви до мястото, doing, после тя се появява. true – още я слага.</summary>
    private bool UpdatePlacing(double now, double dt)
    {
        if (_placing is not { } kind) return false;
        if (_placingAt == 0)
        {
            if (WalkStep(_placingStand, dt)) return true;
            _facingLeft = _placingX < _x;
            _anim = IsExhausted && _lib.Has("doing_tired") ? "doing_tired" : "doing";
            _animTime = 0;
            _placingAt = now + 1.4;
            return true;
        }
        if (now < _placingAt) return true;
        _placing = null;
        CoreEngine.Bus.Send(new PlaceItem(kind, PlacedBy.Bear, _placingX));
        if (_scene != null && FurnishingOf(kind) is { } f) _sceneNear = FurnitureAnchor(f, _scene);
        _anchorWalk = true;    // после застава до/върху нея (FollowSceneAnchor)
        return false;
    }

    /// <summary>Една стъпка към spot (с ходенето). true – още върви.</summary>
    private bool WalkStep(double spot, double dt)
    {
        if (Math.Abs(_x - spot) <= PixelSize) return false;
        double dir = Math.Sign(spot - _x);
        _facingLeft = dir < 0;
        _x += dir * Math.Min(Math.Abs(spot - _x), (IsExhausted ? WalkSpeed * 0.6 : WalkSpeed) * dt);
        string walk = IsExhausted ? "walk_tired" : "walk";
        if (_anim != walk)
        {
            _anim = walk;
            _animTime = 0;
        }
        return true;
    }

    // ── Тут слага нещо: мечокът отива и го слага ──

    private string? _handKind;
    private Action<double>? _handPlace;
    private double _handX, _handUntil;

    /// <summary>Тут го носи ли в момента (от „Неща на Мечо“) – менюто го показва като сложено.</summary>
    public bool Carrying(string kind) => _handKind == kind;

    /// <summary>
    /// Тут сложи нещо от менюто: свободен ли е – отива на свободно място, слага го (doing) и
    /// чак тогава то се появява; зает ли е (спи, работи, яде, готви, в кътче) – появява се
    /// веднага до него. place(x) е самото слагане.
    /// </summary>
    private void PlaceByHand(string kind, double width, Action<double> place)
    {
        if (_handKind != null) FinishHand();
        bool free = _state is BearState.Idle or BearState.Walk && !_inScene && !IsAsleep && !InFocus && _eating == null
            && !Cooks && _pendingCook == null && !OnWalk && !InstantModes && IsVisible;
        if (!free)
        {
            place(NearBear(width));
            return;
        }
        double c = FreeSpot(width, SpriteWidth * 0.3);
        int side = c >= _x ? 1 : -1;
        var area = AreaAt(c, _y - 1);
        double stand = Math.Clamp(c - side * (width / 2 + SpriteWidth * 0.35), area.Left + SpriteWidth / 2, area.Right - SpriteWidth / 2);
        _handKind = kind;
        _handPlace = place;
        _handX = c;
        _handUntil = Now + 25;   // ако нещо го прекъсне – все пак се появява
        WalkTo(stand, () =>
        {
            _facingLeft = c < _x;
            Play(IsExhausted && _lib.Has("doing_tired") ? "doing_tired" : "doing", length: 1.3, after: FinishHand);
        });
    }

    /// <summary>Сложи го (или – прекъснат е – просто се появява).</summary>
    private void FinishHand()
    {
        if (_handPlace is not { } place) return;
        double x = _handX;
        _handKind = null;
        _handPlace = null;
        place(x);
        NotifyCare();
    }

    /// <summary>На кадър: нещо го прекъсна, докато носеше нещо (вдигнаха го, заспа) – появява се.</summary>
    private void UpdateHand(double now)
    {
        if (_handKind == null) return;
        bool going = _state is BearState.Walk or BearState.Busy && now < _handUntil;
        if (!going) FinishHand();
    }

    /// <summary>„Неща на Мечо“: Тут слага нещо (мечокът го носи) или го прибира (веднага).</summary>
    public void ToggleFromMenu(string kind)
    {
        if (_handKind == kind)
        {
            // Цъкна пак, докато го носи: отказва се.
            _handKind = null;
            _handPlace = null;
            NotifyCare();
            return;
        }
        bool on = kind switch
        {
            Kitchen.Heater => _save.HeaterPlaced,
            Kitchen.Lamp => LampOn,
            Kitchen.Radio => RadioPlaced,
            Kitchen.Feeder => FeederPlaced,
            Kitchen.PlantPot => PlantOut,
            _ => FurniturePlaced(kind),
        };
        if (on)
        {
            switch (kind)
            {
                case Kitchen.Heater: SetHeaterPlaced(false); break;
                case Kitchen.Lamp: SetLamp(false); break;
                case Kitchen.Radio: ToggleRadioPlaced(); break;
                case Kitchen.Feeder: SetFeeder(false); break;
                case Kitchen.PlantPot: TogglePlant(); break;
                default:
                    // Сложена от мечока (ползва я) или от Тут – прибира се веднага (заниманието спира само).
                    SetFurniture(kind, false);
                    break;
            }
            return;
        }
        if (!Kitchen.Owns(_save, kind)) return;
        double width = kind switch
        {
            Kitchen.Heater => 20 * PixelSize,
            Kitchen.Lamp => 14 * PixelSize,
            Kitchen.Radio => 14 * PixelSize,
            Kitchen.Feeder => 16 * PixelSize,
            Kitchen.PlantPot => PlantW * PixelSize,
            _ => FurnitureWidth(FurnishingOf(kind)!),
        };
        var bus = CoreEngine.Bus;
        PlaceByHand(kind, width, x =>
        {
            switch (kind)
            {
                case Kitchen.Heater: SetHeaterPlaced(true); break;
                case Kitchen.Lamp: SetLamp(true); break;
                case Kitchen.Radio: SetRadio(true); break;
                case Kitchen.Feeder: SetFeeder(true); break;
                case Kitchen.PlantPot: SetPlant(true); break;
                default: SetFurniture(kind, true, x); return;
            }
            bus.Send(new MoveItem(kind, x));   // там, където го сложи (не на старото място)
            Persist();
        });
        NotifyCare();
    }

    // ── Леглото ──

    /// <summary>
    /// Ляга да спи: има ли легло – отива до него (сложено е) или първо го слага до себе си
    /// (doing) и ляга в него; после yawn → sleep.
    /// </summary>
    private void GoToBed()
    {
        var bed = FurnishingOf(Kitchen.Bed);
        if (bed == null || !Kitchen.Owns(_save, Kitchen.Bed))
        {
            Play("yawn", then: "sleep");
            return;
        }
        double w = FurnitureWidth(bed);
        if (!FurniturePlaced(Kitchen.Bed))
        {
            if (InstantModes || !IsVisible)
            {
                CoreEngine.Bus.Send(new PlaceItem(Kitchen.Bed, PlacedBy.Bear, Math.Clamp(_x, Area.Left + w / 2, Math.Max(Area.Left + w / 2, Area.Right - w / 2))));
                if (InstantModes) _x = PlacementOf(Kitchen.Bed).X;
                Play("yawn", then: "sleep");
                return;
            }
            double c = FreeSpot(w, SpriteWidth * 0.3);
            int side = c >= _x ? 1 : -1;
            var area = AreaAt(c, _y - 1);
            double stand = Math.Clamp(c - side * (w / 2 + SpriteWidth * 0.3), area.Left + SpriteWidth / 2, area.Right - SpriteWidth / 2);
            WalkTo(stand, () =>
            {
                _facingLeft = c < _x;
                Play("doing", length: 1.4, after: () =>
                {
                    CoreEngine.Bus.Send(new PlaceItem(Kitchen.Bed, PlacedBy.Bear, c));
                    WalkTo(c, () => Play("yawn", then: "sleep"));
                });
            });
            return;
        }
        double bx = PlacementOf(Kitchen.Bed).X;
        if (InstantModes || double.IsNaN(bx) || Math.Abs(_x - bx) <= PixelSize * 2)
        {
            if (InstantModes && !double.IsNaN(bx)) _x = bx;
            Play("yawn", then: "sleep");
            return;
        }
        WalkTo(bx, () => Play("yawn", then: "sleep"));
    }

    /// <summary>Цъкна леглото → „Лягай да спиш“: колко да спи (като в менюто) или свое число минути.</summary>
    internal void AskSleepTime()
    {
        if (IsAsleep) return;
        var options = new List<ChatOption>();
        if (LateForBed) options.Add(new ChatOption("🌙 Лягай за деня", p => p.SleepForDay()));
        foreach (var (label, minutes) in new[] { ("20 мин", 20), ("1 ч", 60), ("2 ч", 120) })
        {
            int m = minutes;
            options.Add(new ChatOption(label, p => p.Sleep(m)));
        }
        options.Add(new ChatOption("Не сега", _ => { }));
        Ask(new ChatQuestion("Колко да поспя?", options.ToArray())
        {
            FreePrompt = "Или напиши колко минути:",
            FreeButton = "Спи",
            FreeText = (p, text) =>
            {
                if (double.TryParse(text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m)
                    && m > 0 && m <= 24 * 60) p.Sleep(m);
                else p.Say("Хм, колко минути? Напиши само число – например 45.", 3);
            },
        });
    }

    /// <summary>Тръгнал е да спи (Тут го сложи), но нещо го прекъсна (вдигна го) – пак отива да легне.</summary>
    private bool GoingToBedAgain()
    {
        if (!_save.SleepingByChoice || IsAsleep || _save.NapUntil <= DateTime.Now) return false;
        GoToBed();
        return true;
    }
}
