using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Растението (саксията, „Хобита“): едно растение в саксия, което расте през 10
// фази – от мъничко кълнче до голямо растение. Видът е скин: слънчоглед (класически),
// монстера, кактус. Стои скрито зад ръба на екрана. Като си почива (или Тут му каже
// „Грижи се за растението“), мечокът отива до най-близкия ръб, издърпва саксията
// и се грижи за нея: полива с лейката (капят сини капчици), докато е малко –
// разкопава с лопатката, а като порасне – подрязва с ножицата. Накрая я бута обратно.
// Расте от грижите: на 8 мин грижа – една фаза (най-много 2 на ден). Без грижи –
// много бавно: една фаза на 5 дни.
public sealed partial class PetWindow
{
    public const int PlantStages = 10;
    private const double PlantW = 24, PlantH = 40;      // рисунката plant_<вид>_<фаза>.png
    private OverlayWindow? _gardenWindow;
    private readonly Image _plant = new() { IsHitTestVisible = false };
    private int _plantSide;            // -1 – левият ръб, +1 – десният
    private double _plantOut;          // 0 – скрита зад ръба, 1 – навън (дърпа я, грижи се, бута я)
    private int _gardenPhase;          // 0 – отива зад ръба, 1 – връща се с саксията, 2 – грижи се
    private double _gardenWaterUntil;  // докога трае сегашната грижа
    private double _gardenLast;
    private string _plantTool = "wateringcan";
    private string? _devPlantTool;

    /// <summary>Developer: изкарва растението (ако не е навън) и прави точно тази грижа.</summary>
    internal void DevPlantCare(string tool)
    {
        if (!InGarden)
        {
            if (!_save.Owned.Contains(Kitchen.PlantPot)) _save.Owned.Add(Kitchen.PlantPot);
            InstantModes = true;
            Relax(Scenes.Plant);
            InstantModes = false;
        }
        _devPlantTool = tool;
        if (_gardenPhase >= 2) NextCare(Now);
    }

    private bool InGarden => _inScene && _scene == Scenes.Plant;

    /// <summary>Видът растение (скинът на саксията): sunflower, monstera, cactus.</summary>
    public string PlantKind => Furniture.Current(_save, Kitchen.PlantPot).Id is var id && id != Furniture.Classic ? id : "sunflower";

    private string PlantArt => $"plant_{PlantKind}_{Math.Clamp(_save.PlantStage, 0, PlantStages - 1)}";

    // Капчиците от лейката (в прозореца на мечока; x/y са на екрана).
    private sealed class Drop
    {
        public readonly System.Windows.Shapes.Rectangle Shape = new() { IsHitTestVisible = false };
        public double X, Y, Vx, Vy, Ground;
    }

    private readonly List<Drop> _drops = new();
    private double _nextDrop;
    private static readonly Brush[] DropBrushes = MakeDropBrushes();

    private static Brush[] MakeDropBrushes()
    {
        var list = new[] { Color.FromRgb(90, 140, 210), Color.FromRgb(130, 180, 235), Color.FromRgb(70, 120, 200) }
            .Select(c => (Brush)new SolidColorBrush(c)).ToArray();
        foreach (var b in list) b.Freeze();
        return list;
    }

    /// <summary>Докато полива: нови капчици от чучура на лейката; падат до земята.</summary>
    private void UpdateDrops(double now, double dt)
    {
        bool watering = InGarden && _gardenWaterUntil > now && _anim == "doing" && _plantTool == "wateringcan";
        if (watering && now >= _nextDrop && !_bearRect.IsEmpty)
        {
            _nextDrop = now + 0.06 + _rng.NextDouble() * 0.08;
            var (px, py) = ToolPoint() ?? (24, 14);
            px += 6;   // чучурът е вдясно от дръжката
            if (_facingLeft) px = _lib.FrameWidth - 1 - px;
            var d = new Drop
            {
                X = _bearRect.Left + (px + _rng.NextDouble()) * PixelSize,
                Y = _bearRect.Top + (py + 1) * PixelSize,
                // Хвърчат напред (накъдето гледа – към саксията), после падат.
                Vx = (_facingLeft ? -1 : 1) * PixelSize * (16 + _rng.NextDouble() * 18),
                Vy = -PixelSize * (4 + _rng.NextDouble() * 10),
                Ground = _y,
            };
            d.Shape.Width = d.Shape.Height = PixelSize;
            d.Shape.Fill = DropBrushes[_rng.Next(DropBrushes.Length)];
            _root.Children.Add(d.Shape);
            _drops.Add(d);
        }
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            d.Vy += PixelSize * 160 * dt;
            d.X += d.Vx * dt;
            d.Y += d.Vy * dt;
            if (d.Y >= d.Ground - PixelSize || !InGarden)
            {
                _root.Children.Remove(d.Shape);
                _drops.RemoveAt(i);
            }
        }
    }

    private void PlaceDrops(double dpi)
    {
        foreach (var d in _drops) Place(d.Shape, new Rect(d.X, d.Y, PixelSize, PixelSize), _window.Left, _window.Top, dpi);
    }

    /// <summary>Точката на предмета в лапата (от _tool слоя) за сегашния кадър.</summary>
    private (int X, int Y)? ToolPoint()
    {
        var strip = _lib.Layer(_anim, "tool");
        if (strip == null || strip.Length == 0) return null;
        return Dresser.FirstPoint(strip[_lib.Get(_anim).IndexAt(_animTime) % strip.Length]);
    }

    /// <summary>Отива към по-близкия ръб, излиза зад него и се връща, като дърпа саксията след себе си.</summary>
    private void StartGarden()
    {
        var area = Area;
        _plantSide = _x - area.Left < area.Right - _x ? -1 : 1;
        _gardenPhase = 0;
        _plantOut = 0;
        if (_save.PlantPlaced)
        {
            // Саксията е сложена на земята: отива направо до нея (от страната, откъдето идва).
            _plantSide = _x < _save.PlantX ? 1 : -1;
            _gardenPhase = 1;
            _plantOut = 1;
        }
        _plantPushing = false;
        _gardenWaterUntil = 0;
        _gardenLast = Now;
        GrowPlantByTime();
        ShowGarden();
    }

    /// <summary>Мястото на саксията, когато е издърпана (до ръба, вътре в екрана).</summary>
    private double PlantSpot => _save.PlantPlaced ? _save.PlantX
        : _plantSide < 0 ? Area.Left + PlantW * PixelSize * 0.7 : Area.Right - PlantW * PixelSize * 0.7;

    /// <summary>Колко встрани от мечока е саксията, докато я дърпа/бута (откъм ръба).</summary>
    private double PlantGap => PlantW * PixelSize * 0.5 + SpriteWidth * 0.32;

    /// <summary>Къде застава мечокът до саксията (откъм екрана).</summary>
    private double CareSpot => PlantSpot - _plantSide * PlantGap;

    /// <summary>Има ли рисунки „носи нещо пред себе си“ (hold, hold_walk) – тогава носи саксията, а не я дърпа.</summary>
    private bool CarriesPlant => _lib.Has("hold_walk") && !_save.PlantPlaced;

    private string HoldWalk => "hold_walk";

    /// <summary>Докъде я носи: с нея в лапите застава така, че като я остави, тя е на мястото си.</summary>
    private double CarrySpot => CarriesPlant ? PlantSpot - _plantSide * CarryGap : CareSpot;

    /// <summary>Колко пред мечока е саксията, докато я носи.</summary>
    private double CarryGap => SpriteWidth * 0.3;

    /// <summary>Носи ли я сега в лапите (на път навън или обратно).</summary>
    private bool PlantInPaws => CarriesPlant && ((InGarden && _gardenPhase == 1) || _plantPushing);

    /// <summary>Зад ръба: там мечокът „взима“ саксията (извън екрана).</summary>
    private double BehindEdge => _plantSide < 0 ? Area.Left - SpriteWidth * 0.6 : Area.Right + SpriteWidth * 0.6;

    private void ShowGarden()
    {
        if (_gardenWindow == null)
        {
            RenderOptions.SetBitmapScalingMode(_plant, BitmapScalingMode.NearestNeighbor);
            _gardenWindow = new OverlayWindow(_plant);
        }
        DrawGarden();
    }

    /// <summary>Саксията: до мечока откъм ръба (докато я дърпа/бута) или на мястото си.</summary>
    private void DrawGarden()
    {
        if (_gardenWindow == null) return;
        _plant.Source = _lib.GetProp(PlantArt, (int)PlantW, (int)PlantH, 0).Image;
        _plant.Width = PlantW * PixelSize;
        _plant.Height = PlantH * PixelSize;
        double center = (_gardenPhase >= 2 || _save.PlantPlaced) && !_plantPushing ? PlantSpot : _x + _plantSide * PlantGap;
        double bottom = _save.PlantPlaced && !_plantPushing ? AreaAt(PlantSpot, _y - 1).Bottom + PixelSize : Ground(_y) + PixelSize;
        if (PlantInPaws)
        {
            // В лапите, пред него (накъдето гледа), малко над земята.
            center = _x + (_facingLeft ? -1 : 1) * CarryGap;
            bottom -= SpriteHeight * 0.12;
        }
        bool shown = _plantOut > 0 && IsVisible;
        if (shown) _gardenWindow.Place(center - _plant.Width / 2, bottom - _plant.Height);
        else _gardenWindow.Conceal();
    }

    private void HideGarden() => _gardenWindow?.Conceal();

    /// <summary>Мечокът е в градинката или бута саксията навън: може да излезе зад ръба.</summary>
    private bool GardenOffscreenOk => InGarden || _plantPushing;

    /// <summary>Всеки кадър: излиза зад ръба, връща се с саксията, после грижи (лейка, лопатка, ножица).</summary>
    private void UpdateGarden(double now)
    {
        double dt = Math.Clamp(now - _gardenLast, 0, 0.1);
        _gardenLast = now;
        if (_gardenWindow == null) ShowGarden();
        switch (_gardenPhase)
        {
            case 0:   // към ръба и зад него
                if (StepGarden(BehindEdge, 1.0, dt)) return;
                _gardenPhase = 1;
                _plantOut = 1;
                return;
            case 1:   // връща се със саксията: носи я пред себе си (hold_walk) или я дърпа след себе си
                if (StepGarden(CarrySpot, CarriesPlant ? 0.8 : 0.6, dt))
                {
                    if (CarriesPlant) _anim = HoldWalk;   // гледа накъдето върви, саксията е в лапите
                    else if (!_save.PlantPlaced) _facingLeft = _plantSide > 0;   // гледа навътре, дърпа назад
                    DrawGarden();
                    return;
                }
                _gardenPhase = 2;
                _facingLeft = _plantSide < 0;
                Mutter(R.Pick(_save.PlantPlaced && R.Has("plant.visit") ? "plant.visit" : "plant.pull", _save.OwnerName), 3);
                DrawGarden();
                NextCare(now);
                return;
        }
        // Грижи се: всяка грижа трае 6–12 с, после следващата.
        _facingLeft = _plantSide < 0;
        _save.PlantCareMinutes += dt / 60;
        if (_save.PlantCareMinutes >= 8) GrowPlant(fromCare: true);
        if (now >= _gardenWaterUntil) NextCare(now);
    }

    /// <summary>Крачка към x (с анимацията за ходене); true – още не е стигнал.</summary>
    private bool StepGarden(double target, double speed, double dt)
    {
        if (Math.Abs(target - _x) <= 1) return false;
        double dir = Math.Sign(target - _x);
        _facingLeft = dir < 0;
        _x += dir * Math.Min(Math.Abs(target - _x), WalkSpeed * 0.8 * speed * dt);
        string walk = PlantInPaws ? HoldWalk : IsExhausted ? "walk_tired" : "walk";
        if (_anim != walk) { _anim = walk; _animTime = 0; }
        return true;
    }

    /// <summary>Следващата грижа: полива; малко – разкопава с лопатката; голямо – подрязва; понякога се любува.</summary>
    private void NextCare(double now)
    {
        // Developer може да избере следващата грижа (лейка, лопатка, ножица или „любува се“).
        string? forced = _devPlantTool;
        _devPlantTool = null;
        int roll = forced == null ? _rng.Next(10) : forced == "admire" ? 9 : 0;
        bool small = _save.PlantStage < 5;
        if (forced is "wateringcan" or "shovel" or "scissors") _plantTool = forced;
        else if (roll < 4) _plantTool = "wateringcan";
        else if (roll < 8) _plantTool = small ? "shovel" : "scissors";
        else
        {
            _anim = "happy";
            _animTime = 0;
            _gardenWaterUntil = now + 3;
            Mutter(R.Pick("garden.admire", _save.OwnerName), 4);
            return;
        }
        _anim = "doing";
        _animTime = 0;
        _gardenWaterUntil = now + 6 + _rng.NextDouble() * 6;
    }

    /// <summary>Като свърши: бута саксията обратно зад ръба и се връща (вика се от LeaveScene).</summary>
    private void PutPlantAway()
    {
        // Сложена от Тут: остава на мястото си.
        if (_save.PlantPlaced)
        {
            _plantShownAsVisible = !IsVisible;   // ще се нарисува наново на мястото си
            return;
        }
        if (_gardenWindow == null || _plantOut <= 0 || _gardenPhase < 1)
        {
            HideGarden();
            _plantOut = 0;
            return;
        }
        _plantPushing = true;
        double inside = _plantSide < 0 ? Area.Left + SpriteWidth * 1.6 : Area.Right - SpriteWidth * 1.6;
        // Бута я (или я носи в лапите) до зад ръба, после се връща в екрана.
        Later(0.05, () => WalkTo(BehindEdge - _plantSide * PlantGap * 0.2, () =>
        {
            _plantPushing = false;
            _plantOut = 0;
            HideGarden();
            WalkTo(inside);
        }));
    }

    private bool _plantPushing;

    /// <summary>Докато я бута: саксията е пред него (откъм ръба). Вдигнат ли е – саксията се прибира сама.</summary>
    private void UpdatePlantAway(double dt)
    {
        if (!_plantPushing) return;
        if (_state != BearState.Walk && _state != BearState.Idle)
        {
            _plantPushing = false;
            _plantOut = 0;
            HideGarden();
            return;
        }
        if (CarriesPlant && _state == BearState.Walk && _anim != HoldWalk)
        {
            _anim = HoldWalk;
            _animTime = 0;
        }
        DrawGarden();
    }

    /// <summary>Една фаза нагоре (от грижа – най-много 2 на ден).</summary>
    private void GrowPlant(bool fromCare)
    {
        string today = DayKey(DateTime.Today);
        if (_save.PlantGrowDay != today)
        {
            _save.PlantGrowDay = today;
            _save.PlantGrowToday = 0;
        }
        if (fromCare)
        {
            _save.PlantCareMinutes = 0;
            if (_save.PlantGrowToday >= 2) return;
            _save.PlantGrowToday++;
        }
        _save.PlantLastGrow = DateTime.Now;
        if (_save.PlantStage >= PlantStages - 1) { Persist(); return; }
        _save.PlantStage++;
        if (InGarden)
        {
            Emote(_save.PlantStage == PlantStages - 1 ? "sparkle" : "heart", 2.5);
            Say(R.Pick(_save.PlantStage == PlantStages - 1 ? "garden.bloom" : "plant.grow", _save.OwnerName), 4);
        }
        DrawGarden();
        Persist();
    }

    /// <summary>Без грижи расте много бавно: една фаза на 5 дни.</summary>
    private void GrowPlantByTime()
    {
        if (!Kitchen.Owns(_save, Kitchen.PlantPot)) return;
        if (_save.PlantLastGrow == DateTime.MinValue) _save.PlantLastGrow = DateTime.Now;
        while (DateTime.Now - _save.PlantLastGrow > TimeSpan.FromDays(5) && _save.PlantStage < PlantStages - 1)
        {
            _save.PlantStage++;
            _save.PlantLastGrow = _save.PlantLastGrow.AddDays(5);
        }
    }
}
