using System;
using Mecho.Core;

namespace Mecho.Engine;

/// <summary>
/// Сглобява ядрото за приложението и е ЕДИНСТВЕНОТО място, където се изброяват
/// модулите. Нова механика:
///   1) клас XModule : GameModule (в src/Mecho/Engine/Modules или в Mecho.Core, ако е без WPF);
///   2) един ред тук: engine.Modules.Add(new XModule(...));
///   3) стъпка в SelfTest и тест в tests/Mecho.Core.Tests.
/// Модулите не се реферират един друг – говорят през engine.Bus и engine.Store.
/// </summary>
public static class CoreSetup
{
    public static GameEngine Build(PetWindow pet, SaveData save, Action<Action> marshal)
    {
        var memory = new MemoryLog(400);
        var engine = new GameEngine(log: new TeeLog(memory, new CrashLogAdapter()), marshal: marshal);
        Memory = memory;

        // Запазеното състояние на ядрото (слоевете на модулите) – преди модулите да се
        // регистрират, за да получат своите данни.
        if (save.Core != null) engine.Store.Import(save.Core);

        // ── Модулите (по реда на добавяне; зависимостите се подреждат сами) ──
        engine.Modules.Add(new LegacyBridgeModule(pet, save));

        return engine;
    }

    /// <summary>Последните записи на ядрото (Developer → „Ядро: лог“).</summary>
    public static MemoryLog? Memory { get; private set; }

    /// <summary>Грешките на ядрото отиват и в crash.log (Тут го праща при проблем).</summary>
    private sealed class CrashLogAdapter : ICoreLog
    {
        public void Info(string message) { }

        public void Warn(string message) => CrashLog.Note("ядро: " + message);

        public void Error(string message, Exception? ex = null)
        {
            if (ex != null) CrashLog.Write(new Exception("ядро: " + message, ex));
            else CrashLog.Note("ядро: " + message);
        }
    }
}
