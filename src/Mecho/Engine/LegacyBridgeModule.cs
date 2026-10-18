using System;
using Mecho.Core;

namespace Mecho.Engine;

/// <summary>
/// ВРЕМЕНЕН мост между стария код (PetWindow) и ядрото, докато поведението на мечока
/// не мине в модули. Веднъж в секунда съобщава какво прави мечокът (ReportBearStatus –
/// спи ли, работи ли, навън ли е, кое занимание), за да могат модулите (нуждите,
/// бъдещите събития) да го четат от BearStatusState, без да познават прозореца.
/// Данните (лешници, притежавано, предмети, нужди) вече са в ядрото – мостът не ги пипа.
/// </summary>
public sealed class LegacyBridgeModule : GameModule
{
    private readonly PetWindow _pet;

    public LegacyBridgeModule(PetWindow pet, SaveData save)
    {
        _pet = pet ?? throw new ArgumentNullException(nameof(pet));
        ArgumentNullException.ThrowIfNull(save);
    }

    public override string Id => "legacy.bridge";

    public override System.Collections.Generic.IReadOnlyCollection<string> DependsOn { get; } = new[] { BearStatusModule.ModuleId };

    protected override void OnRegister() => Every("status", 1, 1, Report);

    internal void Report()
    {
        var (asleep, working, outside, activity) = _pet.StatusForCore;
        Send(new ReportBearStatus(asleep, working, outside, activity));
    }
}
