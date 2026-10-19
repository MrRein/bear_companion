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

    public string? CurrentEmote => _emote.Visibility == Visibility.Visible ? _emoteName : null;

    private void InitEmotes()
    {
        RenderOptions.SetBitmapScalingMode(_emote, BitmapScalingMode.NearestNeighbor);
        _emote.RenderTransform = _emotePop;
        _emote.RenderTransformOrigin = new Point(0.2, 1);
        _root.Children.Add(_emote);
    }

    /// <summary>Показва емоция до главата (сменя предишната).</summary>
    public void Emote(string name, double seconds = 2.6)
    {
        if (Ui.LoadIcon("emote_" + name) is not { } raw) return;
        // Балончето е в цветовете на менюто (сменя се със скина му).
        var img = Ui.Themed(raw);
        _emoteName = name;
        _emote.Source = img;
        _emote.Width = img.PixelWidth * EmoteScale;
        _emote.Height = img.PixelHeight * EmoteScale;
        _emoteStart = Now;
        _emoteUntil = Now + seconds;
        _emote.Visibility = Visibility.Visible;
    }

    /// <summary>Колкото пикселите на мечока – следва мащаба.</summary>
    private double EmoteScale => PixelSize;

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
        _emote.Opacity = Math.Clamp((_emoteUntil - now) / 0.4, 0, 1);
        double bob = Math.Round(Math.Sin(t * 4) * PixelSize * 0.5);
        // Опашчицата (долу вляво на балончето) сочи към главата: балончето е горе вдясно
        // до ухото; до десния ръб на екрана – огледално, отляво.
        double e = EmoteScale;
        bool left = bear.Left + 25 * PixelSize + 11 * e > Area.Right - 4;
        _emotePop.ScaleY = pop;
        _emotePop.ScaleX = left ? -pop : pop;
        double x = left ? bear.Left + 7 * PixelSize - 10.5 * e : bear.Left + 25 * PixelSize - 2.5 * e;
        double top = bear.Top + 13 * PixelSize - 12.5 * e + bob;
        var origin = new Point(0.2, 1);   // от опашчицата (огледалното обръща и нея)
        if (_emote.RenderTransformOrigin != origin) _emote.RenderTransformOrigin = origin;
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
