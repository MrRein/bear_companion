using Mecho.Core;

namespace Mecho.Core.Tests;

// Общи неща за тестовете: двигател с ръчен часовник и фиксирана случайност.
internal static class Kit
{
    public static (GameEngine Engine, ManualClock Clock, MemoryLog Log) Engine(DateTime? start = null)
    {
        var clock = new ManualClock(start ?? new DateTime(2026, 10, 18, 10, 0, 0));
        var log = new MemoryLog();
        var engine = new GameEngine(clock, new SystemRandom(42), log);
        return (engine, clock, log);
    }
}

internal sealed record Ping(int N) : ISignal;
internal sealed record Pong(int N) : ISignal;
internal sealed record DoThing(string What) : IGameCommand;
internal sealed record Counter(int Value) : IStateSlice;
internal sealed record Bag(IReadOnlyList<string> Items) : IStateSlice;
