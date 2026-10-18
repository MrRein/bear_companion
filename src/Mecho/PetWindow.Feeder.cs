using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

// Хранилката за птички (купува се в „Мебели“): Тут я слага от „Сложи“ някъде на
// земята. Празна ли е, мечокът понякога отива и слага семенца. Докато има храна,
// рядко (на 20–50 мин) долита птичка – врабче, червеношийка, синигер, червенушка
// или синя птичка – кълве малко и отлита (всяка изяжда една порция от три).
// Цъкнеш ли хранилката: Сложи храна / Махни / Остави. Скинове: тръба, къщичка…
public sealed partial class PetWindow
{
    private CookerWindow? _feederWindow;
    private OverlayWindow? _birdWindow;
    private readonly Image _bird = new() { IsHitTestVisible = false };
    private readonly ScaleTransform _birdFlip = new(1, 1);
    private string _birdKind = "sparrow";
    private int _birdPhase;                 // 0 – няма, 1 – долита, 2 – кълве, 3 – отлита
    private double _birdX, _birdY, _birdFromX, _birdFromY, _birdT, _birdUntil, _nextBird = 15 * 60;
    private int _feederShownFood = -1;

    private static readonly string[] BirdKinds = { "sparrow", "robin", "tit", "bullfinch", "bluebird" };
    private const double BirdArt = 10;      // рисунката на птичката е 10×10

    public bool FeederPlaced => Kitchen.Owns(_save, Kitchen.Feeder) && _save.FeederPlaced;

    public void SetFeeder(bool placed)
    {
        if (!Kitchen.Owns(_save, Kitchen.Feeder)) return;
        if (placed && !_save.FeederPlaced) _save.FeederX = double.NaN;   // до мечока, не там, където е била
        _save.FeederPlaced = placed;
        Persist();
        NotifyCare();
    }

    private string FeederArtName => Furniture.CurrentArt(_save, Kitchen.Feeder, _save.FeederFood > 0 ? "_food" : "");

    private void UpdateFeeder(double now, double dt)
    {
        if (!FeederPlaced)
        {
            _feederWindow?.Close();
            _feederWindow = null;
            _birdWindow?.Conceal();
            _birdPhase = 0;
            return;
        }
        if (_feederWindow == null)
        {
            var area = Area;
            double x = double.IsNaN(_save.FeederX) ? _x + (_x > area.Left + area.Width / 2 ? -1 : 1) * SpriteWidth * 1.6 : _save.FeederX;
            x = Math.Clamp(x, area.Left + 30, area.Right - 30);
            _feederWindow = new CookerWindow("Хранилка");
            Companion(_feederWindow);
            _feederWindow.SetTime(null);
            _feederWindow.Hint = "Хранилката за птички.";
            _feederWindow.Clicked += () => Ask(new ChatQuestion(_save.FeederFood > 0 ? "Хранилката има семенца. Какво да правим?" : "Хранилката е празна. Да сложа ли семенца?",
                new ChatOption("🌻 Сложи храна", p => p.FillFeeder()),
                new ChatOption("Махни", p => p.SetFeeder(false)),
                new ChatOption("Остави", _ => { })));
            _feederWindow.Dropped += (cx, _) =>
            {
                var a = AreaAt(cx, _feederWindow!.Top + _feederWindow.ActualHeight / 2);
                double nx = Math.Clamp(cx, a.Left + 30, a.Right - 30);
                _feederWindow.FallTo(nx, a.Bottom);
                _save.FeederX = nx;
                Persist();
            };
            _feederWindow.SetAway(!IsVisible);
            _feederWindow.Place(_lib.GetProp(FeederArtName, 16, 28, 0).Image, PixelSize, x, area.Bottom);
            _save.FeederX = x;
            _feederShownFood = _save.FeederFood;
        }
        _feederWindow.Step(0.033);
        if ((_save.FeederFood > 0) != (_feederShownFood > 0))
        {
            _feederShownFood = _save.FeederFood;
            _feederWindow.SetArt(_lib.GetProp(FeederArtName, 16, 28, 0).Image, PixelSize);
        }
        UpdateBird(now, dt);
    }

    /// <summary>„Сложи храна“: мечокът отива до хранилката и я пълни.</summary>
    public void FillFeeder()
    {
        if (_feederWindow == null || IsAsleep || OnWalk) return;
        if (_inScene) LeaveScene();
        double cx = _feederWindow.CenterX;
        int side = _x < cx ? -1 : 1;
        WalkTo(cx + side * (SpriteWidth * 0.45 + 4 * PixelSize), () =>
        {
            _facingLeft = side > 0;
            PlayNear(FeederAnchor, "doing", length: 3.5, after: () =>
            {
                _save.FeederFood = 3;
                Persist();
                Announce(R.Pick("feeder.fill", _save.OwnerName), 4);
            });
        });
    }

    private void UpdateBird(double now, double dt)
    {
        if (_feederWindow == null) return;
        if (_birdPhase == 0)
        {
            if (now < _nextBird || _save.FeederFood <= 0 || !IsVisible || OnWalk) return;
            _nextBird = now + Minutes(20, 50);
            StartBird();
            return;
        }
        double perchX = _feederWindow.CenterX - BirdArt * PixelSize / 2;
        double perchY = _feederWindow.Top + 12 * PixelSize - BirdArt * PixelSize;   // на поличката под покрива
        string frame;
        switch (_birdPhase)
        {
            case 1:   // долита по дъга
                _birdT = Math.Min(1, _birdT + dt / 3.5);
                _birdX = _birdFromX + (perchX - _birdFromX) * _birdT;
                _birdY = _birdFromY + (perchY - _birdFromY) * _birdT - Math.Sin(Math.PI * _birdT) * 60;
                frame = (int)(now * 8) % 2 == 0 ? "fly1" : "fly2";
                if (_birdT >= 1)
                {
                    _birdPhase = 2;
                    _birdUntil = now + 20 + _rng.Next(25);
                    if (!IsAsleep && !InFocus && !IsQuiet && _rng.Next(2) == 0) Mutter(R.Pick("feeder.bird", _save.OwnerName), 4);
                }
                break;
            case 2:   // кълве
                _birdX = perchX;
                _birdY = perchY;
                frame = (int)(now * 2.5) % 3 == 0 ? "peck" : "sit";
                if (now >= _birdUntil)
                {
                    _save.FeederFood = Math.Max(0, _save.FeederFood - 1);
                    Persist();
                    _birdPhase = 3;
                    _birdT = 0;
                    _birdFromX = _birdX;
                    _birdFromY = _birdY;
                    _birdFlip.ScaleX = _birdFromX < Area.Left + Area.Width / 2 ? -1 : 1;
                }
                break;
            default:  // отлита нагоре и встрани
                _birdT += dt / 3;
                double dir = _birdFlip.ScaleX;
                _birdX = _birdFromX + dir * _birdT * 400;
                _birdY = _birdFromY - _birdT * 250;
                frame = (int)(now * 8) % 2 == 0 ? "fly1" : "fly2";
                if (_birdT >= 1)
                {
                    _birdPhase = 0;
                    _birdWindow?.Conceal();
                    return;
                }
                break;
        }
        _bird.Source = _lib.GetProp($"bird_{_birdKind}_{frame}", 10, 10, 0).Image;
        if (IsVisible) _birdWindow!.Place(_birdX, _birdY);
        else _birdWindow!.Conceal();
    }

    private void StartBird()
    {
        _birdKind = BirdKinds[_rng.Next(BirdKinds.Length)];
        if (_birdWindow == null)
        {
            RenderOptions.SetBitmapScalingMode(_bird, BitmapScalingMode.NearestNeighbor);
            _bird.RenderTransform = _birdFlip;
            _bird.RenderTransformOrigin = new Point(0.5, 0.5);
            _birdWindow = new OverlayWindow(_bird);
            Companion(_birdWindow);
        }
        _bird.Width = _bird.Height = BirdArt * PixelSize;
        var area = Area;
        bool fromLeft = _feederWindow!.CenterX > area.Left + area.Width / 2;
        _birdFromX = fromLeft ? area.Left - 40 : area.Right + 10;
        _birdFromY = area.Top + area.Height * (0.3 + _rng.NextDouble() * 0.3);
        _birdFlip.ScaleX = fromLeft ? 1 : -1;   // рисунката гледа надясно
        _birdT = 0;
        _birdPhase = 1;
    }
}
