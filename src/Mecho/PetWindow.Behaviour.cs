using System;
using System.Linq;
using System.Windows;
using Mecho.Core;

namespace Mecho;

/// <summary>
/// Мечокът и каталогът на поведението (Behaviour): една проверка дали може да прави
/// нещо (CanDo) и реакция, когато предметите се сменят – продаден/махнат предмет
/// спира заниманието, нов скин или мебел се пречертава веднага.
/// </summary>
public partial class PetWindow
{
    /// <summary>
    /// Може ли сега да седне в това кътче: има всичко нужно (Behaviour.Available) и
    /// събитие днес не го забранява (болен – без батут). ВСИЧКИ, които избират занимание, питат тук.
    /// </summary>
    public bool CanDo(Scene scene) => Scenes.IsUnlocked(scene, _save) && !HobbyBlocked(scene);

    /// <summary>
    /// Прозорецът на предмет, храна, подарък… е „собственост“ на мечока: Windows го държи
    /// винаги НАД мечока (мечокът се рисува зад всичко, което е до него).
    /// </summary>
    private void Companion(Window w)
    {
        try
        {
            if (IsLoaded && !ReferenceEquals(w, this)) w.Owner = this;
        }
        catch (InvalidOperationException)
        {
            // Мечокът още не е показан (рядко) – тогава без собственик.
        }
    }

    /// <summary>Абонира се за промените на предметите (вика се веднъж от конструктора).</summary>
    private void WireBehaviour()
    {
        var bus = CoreEngine.Bus;
        bus.Subscribe<StateChanged<InventoryState>>(this, _ => ItemsChanged());
        bus.Subscribe<StateChanged<LooksState>>(this, _ => ItemsChanged());
        bus.Subscribe<ModifiersChanged>(this, _ => ItemsChanged());
    }

    private bool _itemsCheckQueued;

    /// <summary>Предметите (или днешните забрани) се смениха: проверява кътчето след кадъра (веднъж, не за всяка промяна).</summary>
    private void ItemsChanged()
    {
        if (_itemsCheckQueued) return;
        _itemsCheckQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _itemsCheckQueued = false;
            CheckSceneItems();
        });
    }

    /// <summary>Кътчето, в което е, още ли има всичко? Ако не – става; ако да – пречертава (нов скин, купена мебел).</summary>
    private void CheckSceneItems()
    {
        if (!_inScene || _scene == null || !Scenes.All.Contains(_scene)) return;
        if (!CanDo(_scene))
        {
            var scene = _scene;
            LeaveScene();
            SetIdle();
            if (!IsQuiet) Say(R.Pick("behaviour.item.gone", _save.OwnerName).Replace("{какво}", scene.Doing), 4);
            return;
        }
        LoadSceneArt(_scene, _anim);
    }
}
