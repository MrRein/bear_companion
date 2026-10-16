using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Емоции без думи: малко пикселно балонче над главата (сърце, Zzz, крушка, !, ?,
// нота, капка пот, кюфте, снежинка, звездичка). Показва го за 2–3 секунди, с
// малко подскачане при появата. Рисунките са assets/ui/emote_<име>.png
// (tools/make_emotes.py). Понякога вместо да говори, само показва емоция.
public sealed partial class PetWindow
{
    private readonly Image _emote = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly ScaleTransform _emotePop = new(1, 1);
    private double _emoteStart, _emoteUntil, _nextAmbientEmote = 300;
    private string? _emoteName;

    // Говори ли в момента, емоцията е над балончето (в свой малък прозорец), а не
    // зад него до главата.
    private readonly Image _emoteFloat = new() { IsHitTestVisible = false };
    private readonly ScaleTransform _emoteFloatPop = new(1, 1);
    private OverlayWindow? _emoteWindow;
    private Rect _bubbleRect = Rect.Empty;

    public string? CurrentEmote => _emote.Visibility == Visibility.Visible ? _emoteName : null;

    private void InitEmotes()
    {
        RenderOptions.SetBitmapScalingMode(_emote, BitmapScalingMode.NearestNeighbor);
        _emote.RenderTransform = _emotePop;
        _emote.RenderTransformOrigin = new Point(0.2, 1);
        _root.Children.Add(_emote);
        RenderOptions.SetBitmapScalingMode(_emoteFloat, BitmapScalingMode.NearestNeighbor);
        _emoteFloat.RenderTransform = _emoteFloatPop;
        _emoteFloat.RenderTransformOrigin = new Point(0.2, 1);
    }

    /// <summary>След балончето: ако има и емоция, тя отива над балончето (горе вдясно).</summary>
    private void PlaceEmoteOverBubble()
    {
        bool over = _emote.Visibility == Visibility.Visible && !_bubbleRect.IsEmpty && IsVisible;
        _emote.Opacity = over ? 0 : _emote.Opacity;
        if (!over)
        {
            _emoteWindow?.Conceal();
            return;
        }
        _emoteWindow ??= new OverlayWindow(new Border { Child = _emoteFloat, Padding = new Thickness(2) });
        if (!ReferenceEquals(_emoteFloat.Source, _emote.Source)) _emoteFloat.Source = _emote.Source;
        _emoteFloat.Width = _emote.Width;
        _emoteFloat.Height = _emote.Height;
        _emoteFloatPop.ScaleX = _emoteFloatPop.ScaleY = _emotePop.ScaleX;
        _emoteFloat.Opacity = Math.Clamp((_emoteUntil - Now) / 0.4, 0, 1);
        double x = _bubbleRect.Right - _emote.Width * 0.8;
        double y = _bubbleRect.Top - _emote.Height - 2;
        if (y < Area.Top) y = _bubbleRect.Bottom + 2;
        _emoteWindow.Place(x, y);
        NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(_emoteWindow).Handle);
    }

    /// <summary>Показва емоция над главата (сменя предишната).</summary>
    public void Emote(string name, double seconds = 2.6)
    {
        if (Ui.LoadIcon("emote_" + name) is not { } img) return;
        _emoteName = name;
        _emote.Source = img;
        _emote.Width = img.PixelWidth * EmoteScale;
        _emote.Height = img.PixelHeight * EmoteScale;
        _emoteStart = Now;
        _emoteUntil = Now + seconds;
        _emote.Visibility = Visibility.Visible;
    }

    /// <summary>Колкото пикселите на мечока (мечокът е PixelSize на пиксел).</summary>
    private double EmoteScale => Math.Max(3, PixelSize);

    /// <summary>Всеки кадър: подскача при появата, избледнява накрая; къде стои – виж UpdateLayoutAndWindow.</summary>
    /// <summary>Иконката тъкмо изскача или изчезва (тогава се рисува плавно, иначе стига 15 кадъра в секунда).</summary>
    private bool EmotePopping => _emote.Visibility == Visibility.Visible && (Now - _emoteStart < 0.35 || _emoteUntil - Now < 0.45);

    private Rect EmoteRect(Rect bear, double above)
    {
        if (_emote.Visibility != Visibility.Visible) return Rect.Empty;
        double now = Now;
        if (now > _emoteUntil || IsAsleep && _emoteName != "zzz" || !IsVisible)
        {
            _emote.Visibility = Visibility.Collapsed;
            return Rect.Empty;
        }
        double t = now - _emoteStart;
        double pop = t < 0.18 ? 0.5 + t / 0.18 * 0.65 : t < 0.3 ? 1.15 - (t - 0.18) / 0.12 * 0.15 : 1;
        _emotePop.ScaleX = _emotePop.ScaleY = pop;
        _emote.Opacity = Math.Clamp((_emoteUntil - now) / 0.4, 0, 1);
        double bob = Math.Round(Math.Sin(t * 4) * PixelSize * 0.5);
        // Горе вдясно от главата (опашчицата сочи към нея).
        double x = bear.Left + bear.Width * 0.62;
        double top = Math.Min(above, bear.Top + 2 * PixelSize) - _emote.Height + bob;
        return new Rect(x, top, _emote.Width, _emote.Height);
    }

    /// <summary>
    /// Понякога (на 4–9 мин), без думи: показва как се чувства – гладен, сънлив,
    /// студено му е, весел е… Не и докато спи, работи сериозно или говори.
    /// </summary>
    private void UpdateAmbientEmote(double now)
    {
        if (now < _nextAmbientEmote) return;
        _nextAmbientEmote = now + 240 + _rng.Next(300);
        if (IsAsleep || _bubble.Visibility == Visibility.Visible || _state is BearState.Drag or BearState.Falling) return;
        string? e =
            _save.Fullness < 30 ? "hungry"
            : _save.Energy < TiredBelow ? "zzz"
            : Cold && !Comfortable ? "cold"
            : Hot ? "sweat"
            : InFocus ? (_rng.Next(3) == 0 ? "idea" : null)
            : _inScene && (_scene == Scenes.Music || _scene == Scenes.Guitar) ? "note"
            : _rng.Next(3) == 0 ? (_rng.Next(2) == 0 ? "happy" : "heart")
            : null;
        if (e != null) Emote(e);
    }
}
