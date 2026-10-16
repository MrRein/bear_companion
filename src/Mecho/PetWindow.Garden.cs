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

    private bool InGarden => _inScene && _scene == Scenes.Plant;

    /// <summary>Видът растение (скинът на саксията): sunflower, monstera, cactus.</summary>
    public string PlantKind => Furniture.Current(_save, Kitchen.PlantPot).Id is var id && id != Furniture.Classic ? id : "sunflower";

    private string PlantArt => $"plant_{PlantKind}_{Math.Clamp(_save.PlantStage, 0, PlantStages - 1)}";

    // Капчиците от лейката (в прозореца на мечока; x/y са на екрана).
    private sealed class Drop
    {
        public readonly System.Windows.Shapes.Rectangle Shape = new() { IsHitTestVisible = false };
        public double X, Y, Vy, Ground;
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
                Vy = 20 + _rng.NextDouble() * 30,
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
            d.Vy += 600 * dt;
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
        _plantPushing = false;
        _gardenWaterUntil = 0;
        _gardenLast = Now;
        GrowPlantByTime();
        ShowGarden();
    }

    /// <summary>Мястото на саксията, когато е издърпана (до ръба, вътре в екрана).</summary>
    private double PlantSpot => _plantSide < 0 ? Area.Left + PlantW * PixelSize * 0.7 : Area.Right - PlantW * PixelSize * 0.7;

    /// <summary>Колко встрани от мечока е саксията, докато я дърпа/бута (откъм ръба).</summary>
    private double PlantGap => PlantW * PixelSize * 0.5 + SpriteWidth * 0.32;

    /// <summary>Къде застава мечокът до саксията (откъм екрана).</summary>
    private double CareSpot => PlantSpot - _plantSide * PlantGap;

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
        double center = _gardenPhase >= 2 && !_plantPushing ? PlantSpot : _x + _plantSide * PlantGap;
        bool shown = _plantOut > 0 && IsVisible;
        if (shown) _gardenWindow.Place(center - _plant.Width / 2, Ground(_y) - _plant.Height + PixelSize);
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
            case 1:   // връща се, като дърпа саксията след себе си (по-бавно)
                if (StepGarden(CareSpot, 0.6, dt))
                {
                    _facingLeft = _plantSide > 0;   // гледа навътре, дърпа назад
                    DrawGarden();
                    return;
                }
                _gardenPhase = 2;
                _facingLeft = _plantSide < 0;
                Mutter(R.Pick("plant.pull", _save.OwnerName), 3);
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
        string walk = IsExhausted ? "walk_tired" : "walk";
        if (_anim != walk) { _anim = walk; _animTime = 0; }
        return true;
    }

    /// <summary>Следващата грижа: полива; малко – разкопава с лопатката; голямо – подрязва; понякога се любува.</summary>
    private void NextCare(double now)
    {
        int roll = _rng.Next(10);
        bool small = _save.PlantStage < 5;
        if (roll < 4) _plantTool = "wateringcan";
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
        if (_gardenWindow == null || _plantOut <= 0 || _gardenPhase < 1)
        {
            HideGarden();
            _plantOut = 0;
            return;
        }
        _plantPushing = true;
        double inside = _plantSide < 0 ? Area.Left + SpriteWidth * 1.6 : Area.Right - SpriteWidth * 1.6;
        // Бута я до зад ръба (саксията пред него), после се връща в екрана.
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
