using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Поливане („Полей цветето“): до мечока има градинка с три цветя. Той обикаля от
// цвете на цвете и ги полива с лейката. Всяко цвете расте с по един стадий на ден,
// когато го полее (пръст → кълн → листа → пъпка → цвят в различен цвят). Цъфналото
// след време прецъфтява и на мястото му поникват нови семенца.
public sealed partial class PetWindow
{
    private const int GardenSize = 3;
    private OverlayWindow? _gardenWindow;
    private readonly Canvas _gardenCanvas = new();
    private readonly Image[] _flowers = new Image[GardenSize];
    private double _gardenX;           // средата на градинката (на екрана)
    private int _gardenIdx;            // към кое цвете отива / кое полива
    private double _gardenWaterUntil;  // докога полива сегашното
    private double _gardenLast;

    private static readonly string[] FlowerColors = { "pink", "yellow", "red", "purple", "white", "blue" };

    private bool InGarden => _inScene && _scene == Scenes.Plant;

    private void EnsureGardenSave()
    {
        while (_save.Garden.Count < GardenSize) _save.Garden.Add(0);
        while (_save.GardenColors.Count < GardenSize) _save.GardenColors.Add(FlowerColors[_rng.Next(FlowerColors.Length)]);
        while (_save.GardenGrown.Count < GardenSize) _save.GardenGrown.Add("");
    }

    private double FlowerX(int i) => _gardenX + (i - 1) * SpriteWidth * 1.3;

    /// <summary>Мястото, където застава, за да полее цвете i (цветето е вдясно от него).</summary>
    private double WaterSpot(int i) => FlowerX(i) - SpriteWidth * 0.55;

    /// <summary>Сяда в градинката: тя се появява около мястото му.</summary>
    private void StartGarden()
    {
        EnsureGardenSave();
        var area = Area;
        double half = SpriteWidth * 1.9;
        _gardenX = Math.Clamp(_x + SpriteWidth * 0.55, area.Left + half, area.Right - half);
        _gardenIdx = 0;
        _gardenWaterUntil = 0;
        _gardenLast = Now;
        ShowGarden();
    }

    private void ShowGarden()
    {
        if (_gardenWindow == null)
        {
            for (int i = 0; i < GardenSize; i++)
            {
                _flowers[i] = new Image { IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(_flowers[i], BitmapScalingMode.NearestNeighbor);
                _gardenCanvas.Children.Add(_flowers[i]);
            }
            _gardenWindow = new OverlayWindow(_gardenCanvas);
        }
        DrawGarden();
    }

    private string FlowerArt(int i) => _save.Garden[i] >= 4 ? $"flower_4_{_save.GardenColors[i]}" : $"flower_{_save.Garden[i]}";

    private void DrawGarden()
    {
        if (_gardenWindow == null) return;
        double w = 12 * PixelSize, h = 16 * PixelSize;
        double left = FlowerX(0) - w / 2, width = FlowerX(GardenSize - 1) - FlowerX(0) + w;
        _gardenCanvas.Width = width;
        _gardenCanvas.Height = h;
        for (int i = 0; i < GardenSize; i++)
        {
            var img = _lib.GetProp(FlowerArt(i), 12, 16, 0).Image;
            _flowers[i].Source = img;
            _flowers[i].Width = w;
            _flowers[i].Height = h;
            Canvas.SetLeft(_flowers[i], FlowerX(i) - w / 2 - left);
            Canvas.SetTop(_flowers[i], 0);
        }
        if (IsVisible) _gardenWindow.Place(left, _y - h + PixelSize);
        else _gardenWindow.Conceal();
    }

    private void HideGarden() => _gardenWindow?.Conceal();

    /// <summary>Всеки кадър в градинката: върви до следващото цвете, полива го, то расте.</summary>
    private void UpdateGarden(double now)
    {
        double dt = Math.Clamp(now - _gardenLast, 0, 0.1);
        _gardenLast = now;
        if (_gardenWindow == null || !_gardenWindow.IsVisible) ShowGarden();
        double target = WaterSpot(_gardenIdx);
        if (_gardenWaterUntil == 0)
        {
            // Върви към цветето.
            if (Math.Abs(target - _x) > 1)
            {
                double dir = Math.Sign(target - _x);
                _facingLeft = dir < 0;
                _x += dir * Math.Min(Math.Abs(target - _x), WalkSpeed * 0.8 * dt);
                string walk = IsExhausted ? "walk_tired" : "walk";
                if (_anim != walk) { _anim = walk; _animTime = 0; }
                return;
            }
            // Стигна: обръща се към цветето и полива 5–9 с.
            _facingLeft = false;
            _anim = "doing";
            _animTime = 0;
            _gardenWaterUntil = now + 5 + _rng.NextDouble() * 4;
            return;
        }
        if (now < _gardenWaterUntil) return;
        _gardenWaterUntil = 0;
        GrowFlower(_gardenIdx);
        // Следващото (понякога и се спира да се полюбува).
        _gardenIdx = (_gardenIdx + 1) % GardenSize;
        if (_gardenIdx == 0 && _rng.Next(2) == 0)
        {
            _anim = "happy";
            _animTime = 0;
            _gardenWaterUntil = now + 3;   // любува се, после пак обикаля
            _gardenIdx = GardenSize - 1;
            Mutter(R.Pick("garden.admire", _save.OwnerName), 4);
        }
    }

    /// <summary>Полято цвете: ако днес още не е расло – един стадий нагоре.</summary>
    private void GrowFlower(int i)
    {
        EnsureGardenSave();
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_save.GardenGrown[i] == today) return;
        _save.GardenGrown[i] = today;
        if (_save.Garden[i] >= 4)
        {
            // Цъфнало от дни: понякога прецъфтява и поникват нови семенца.
            if (_rng.Next(3) == 0)
            {
                _save.Garden[i] = 1;
                _save.GardenColors[i] = FlowerColors[_rng.Next(FlowerColors.Length)];
                Mutter(R.Pick("garden.replant", _save.OwnerName), 4);
            }
        }
        else
        {
            _save.Garden[i]++;
            if (_save.Garden[i] == 4)
            {
                Emote("sparkle", 3);
                Say(R.Pick("garden.bloom", _save.OwnerName), 4);
            }
            else Emote("heart", 1.6);
        }
        DrawGarden();
        Persist();
    }
}
