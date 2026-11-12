using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Spare windows: furniture and the radio are not closed when put away – they are hidden
// and used again (by title: one window per thing). Creating a transparent topmost window
// was the biggest stutter left (20–150 ms each time he sat at the desk or easel, or the
// radio came out). The owned furniture also gets its window made ahead, once, in idle time
// after start, so even the first time is smooth.
public sealed partial class PetWindow
{
    private readonly Dictionary<string, CookerWindow> _spareWindows = new();

    /// <summary>Hides the window and keeps it for next time; the field is set to null as before.</summary>
    private void Park(ref CookerWindow? window)
    {
        if (window == null) return;
        window.Park();
        _spareWindows[window.Title] = window;
        window = null;
    }

    /// <summary>The kept window with this title (wired already), or null – then make a new one.</summary>
    private CookerWindow? Unpark(string title) =>
        _spareWindows.Remove(title, out var w) && !w.IsClosed ? w : null;

    private IEnumerable<CookerWindow> SpareWindows => _spareWindows.Values;

    /// <summary>
    /// Once, some time after start, in idle time: windows for the owned furniture (and the
    /// radio) are made and warmed one by one, so that the first use is smooth too.
    /// </summary>
    private void WarmSpareWindows()
    {
        if (SelfTest.Running) return;   // the self-test checks parking on its own (deterministic)
        var todo = new Queue<System.Action>();
        foreach (var f in _furnishings)
        {
            var furn = f;
            if (Kitchen.Owns(_save, furn.Kind))
                todo.Enqueue(() =>
                {
                    if (furn.Window != null || _spareWindows.ContainsKey(furn.Title)) return;
                    var w = NewFurnitureWindow(furn);
                    w.Warm();
                    _spareWindows[w.Title] = w;
                });
        }
        if (Kitchen.Owns(_save, Kitchen.Radio))
            todo.Enqueue(() =>
            {
                if (_radioWindow != null || _spareWindows.ContainsKey("Радио")) return;
                var w = NewRadioWindow();
                w.Warm();
                _spareWindows[w.Title] = w;
            });
        void Next()
        {
            if (todo.Count == 0) return;
            todo.Dequeue()();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, (System.Action)Next);
        }
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, (System.Action)Next);
    }
}
