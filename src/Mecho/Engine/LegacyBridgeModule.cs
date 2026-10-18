using System;
using Mecho.Core;

namespace Mecho.Engine;

/// <summary>
/// ВРЕМЕНЕН мост между стария код (PetWindow + SaveData) и шината. Докато механиките
/// не са преместени в модули (фаза 3), той гледа старите данни веднъж в секунда и
/// пуска сигналите, които новите модули ще слушат: NeedsChanged, HazelnutsChanged,
/// SleepChanged. Така новите модули могат да се пишат още сега, а когато една механика
/// мине в модул, тя сама пуска сигнала си и съответният ред тук се трие.
/// </summary>
public sealed class LegacyBridgeModule : GameModule
{
    private readonly PetWindow _pet;
    private readonly SaveData _save;
    private double _fullness, _energy;
    private int _nuts;
    private bool _asleep;

    public LegacyBridgeModule(PetWindow pet, SaveData save)
    {
        _pet = pet ?? throw new ArgumentNullException(nameof(pet));
        _save = save ?? throw new ArgumentNullException(nameof(save));
    }

    public override string Id => "legacy.bridge";

    protected override void OnRegister()
    {
        _fullness = _save.Fullness;
        _energy = _save.Energy;
        _nuts = _save.Hazelnuts;
        _asleep = _pet.IsAsleep;
        Every("watch", 1, 1, Watch);
    }

    /// <summary>Сравнява със запомненото и пуска сигнал за всяка промяна.</summary>
    internal void Watch()
    {
        // Нуждите: сигнал при промяна с поне 1 точка (не всеки кадър).
        if (Math.Abs(_save.Fullness - _fullness) >= 1 || Math.Abs(_save.Energy - _energy) >= 1)
        {
            var signal = new NeedsChanged(_fullness, _save.Fullness, _energy, _save.Energy);
            _fullness = _save.Fullness;
            _energy = _save.Energy;
            Emit(signal);
        }
        if (_save.Hazelnuts != _nuts)
        {
            var signal = new HazelnutsChanged(_nuts, _save.Hazelnuts);
            _nuts = _save.Hazelnuts;
            Emit(signal);
        }
        if (_pet.IsAsleep != _asleep)
        {
            _asleep = _pet.IsAsleep;
            Emit(new SleepChanged(_asleep));
        }
    }
}
