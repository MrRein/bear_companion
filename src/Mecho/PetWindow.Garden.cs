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
    private double _plantX;            // средата на саксията (докато е навън)
    private double _plantOut;          // 0 – скрита зад ръба, 1 – издърпана
    private int _gardenPhase;          // 0 – върви към ръба, 1 – издърпва, 2 – грижи се
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

    /// <summary>Отива към по-близкия ръб; там издърпва саксията и се грижи за нея.</summary>
    private void StartGarden()
    {
        var area = Area;
        _plantSide = _x - area.Left < area.Right - _x ? -1 : 1;
        _gardenPhase = 0;
        _plantOut = 0;
        _gardenWaterUntil = 0;
        _gardenLast = Now;
        GrowPlantByTime();
        ShowGarden();
    }

    /// <summary>Мястото на саксията, когато е издърпана (до ръба, вътре в екрана).</summary>
    private double PlantSpot => _plantSide < 0 ? Area.Left + PlantW * PixelSize * 0.7 : Area.Right - PlantW * PixelSize * 0.7;

    /// <summary>Къде застава мечокът до саксията (откъм екрана).</summary>
    private double CareSpot => PlantSpot - _plantSide * (PlantW * PixelSize * 0.5 + SpriteWidth * 0.32);

    private void ShowGarden()
    {
        if (_gardenWindow == null)
        {
            RenderOptions.SetBitmapScalingMode(_plant, BitmapScalingMode.NearestNeighbor);
            _gardenWindow = new OverlayWindow(_plant);
        }
        DrawGarden();
    }

    private void DrawGarden()
    {
        if (_gardenWindow == null) return;
        _plant.Source = _lib.GetProp(PlantArt, (int)PlantW, (int)PlantH, 0).Image;
        _plant.Width = PlantW * PixelSize;
        _plant.Height = PlantH * PixelSize;
        // Скрита: зад ръба (извън екрана); издърпана – до ръба.
        double hidden = _plantSide < 0 ? Area.Left - _plant.Width : Area.Right;
        double shown = PlantSpot - _plant.Width / 2;
        double left = hidden + (shown - hidden) * _plantOut;
        if (IsVisible && _plantOut > 0) _gardenWindow.Place(left, Ground(_y) - _plant.Height + PixelSize);
        else _gardenWindow.Conceal();
    }

    private void HideGarden() => _gardenWindow?.Conceal();

    /// <summary>Всеки кадър: върви до ръба, издърпва саксията, после грижи (лейка, лопатка, ножица).</summary>
    private void UpdateGarden(double now)
    {
        double dt = Math.Clamp(now - _gardenLast, 0, 0.1);
        _gardenLast = now;
        if (_gardenWindow == null) ShowGarden();
        switch (_gardenPhase)
        {
            case 0:
            {
                double target = CareSpot;
                if (Math.Abs(target - _x) > 1)
                {
                    double dir = Math.Sign(target - _x);
                    _facingLeft = dir < 0;
                    _x += dir * Math.Min(Math.Abs(target - _x), WalkSpeed * 0.8 * dt);
                    string walk = IsExhausted ? "walk_tired" : "walk";
                    if (_anim != walk) { _anim = walk; _animTime = 0; }
                    return;
                }
                _gardenPhase = 1;
                _facingLeft = _plantSide < 0;
                _anim = "idle";
                _animTime = 0;
                Mutter(R.Pick("plant.pull", _save.OwnerName), 3);
                return;
            }
            case 1:
                // Издърпва саксията иззад ръба (1,5 с).
                _plantOut = Math.Min(1, _plantOut + dt / 1.5);
                DrawGarden();
                if (_plantOut >= 1)
                {
                    _gardenPhase = 2;
                    NextCare(now);
                }
                return;
        }
        // Грижи се: всяка грижа трае 6–12 с, после следващата.
        _facingLeft = _plantSide < 0;
        _save.PlantCareMinutes += dt / 60;
        if (_save.PlantCareMinutes >= 8) GrowPlant(fromCare: true);
        if (now >= _gardenWaterUntil) NextCare(now);
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

    /// <summary>Като свърши: бута саксията обратно зад ръба (вика се от LeaveScene).</summary>
    private void PutPlantAway()
    {
        if (_gardenWindow == null || _plantOut <= 0)
        {
            HideGarden();
            return;
        }
        _plantPushing = true;
    }

    private bool _plantPushing;

    /// <summary>Бута саксията навън (и след като е станал) – докато изчезне зад ръба.</summary>
    private void UpdatePlantAway(double dt)
    {
        if (!_plantPushing) return;
        _plantOut = Math.Max(0, _plantOut - dt / 1.2);
        DrawGarden();
        if (_plantOut <= 0) _plantPushing = false;
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
