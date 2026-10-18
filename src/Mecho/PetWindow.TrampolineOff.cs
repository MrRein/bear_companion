namespace Mecho;

/// <summary>
/// Батутът е архивиран (archive/trampoline). Тези няколко реда пазят старите
/// проверки валидни – мечокът никога не е на батута.
/// </summary>
public sealed partial class PetWindow
{
    private bool _onTrampoline => false;

    private System.Windows.Window? _trampWindow => null;

    private int _streak;

    private double _bounceUntil;

    public bool IsBouncing => false;

    private void StartTrampoline(double seconds, bool quiet = false) { }

    private void EndBounce(string? line) { }

    private void UpdateBounce(double dt) { }
}
