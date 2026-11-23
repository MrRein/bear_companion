using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

// Хрониката и режисьорът на мислите („живият“ Мечо, фаза 0).
public class AliveTests
{
    private static (GameEngine E, ManualClock Clock, List<ISignal> Signals, ThoughtsModule Talk) Started(DateTime? start = null)
    {
        var (e, clock, _) = Kit.Engine(start);
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new BearStatusModule());
        e.Modules.Add(new JournalModule());
        var talk = new ThoughtsModule();
        e.Modules.Add(talk);
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, clock, signals, talk);
    }

    private static List<string> Said(List<ISignal> s) => s.OfType<SpeakNow>().Select(x => x.Thought.Key).ToList();

    [Fact]
    public void Journal_remembers_moments_and_days_together()
    {
        var (e, clock, signals, _) = Started();
        e.Bus.Send(new Remember("project.done", Facts.Of(("проект", "Горски тайни")), Importance: 8));
        var j = e.Store.Get<JournalState>();
        Assert.Equal("Горски тайни", j.LastOf("project.done")!.Fact("проект"));
        Assert.Single(signals.OfType<MomentRemembered>());
        Assert.Equal(1, j.DaysTogether(clock.Today()));
        Assert.Equal(100, j.DaysTogether(clock.Today().AddDays(99)));
        // Стар запис: първият ден е по-рано.
        e.Bus.Send(new SetFirstDay(clock.Today().AddDays(-9)));
        Assert.Equal(10, e.Store.Get<JournalState>().DaysTogether(clock.Today()));
    }

    [Fact]
    public void Minor_moments_fade_after_90_days_important_stay()
    {
        var (e, clock, _, _) = Started();
        e.Bus.Send(new Remember("small", Importance: 2));
        e.Bus.Send(new Remember("big", Importance: 9));
        clock.Now = clock.Now.AddDays(91);
        e.Bus.Send(new Remember("today", Importance: 1));
        var j = e.Store.Get<JournalState>();
        Assert.Equal(0, j.Count("small"));
        Assert.Equal(1, j.Count("big"));
        Assert.Equal(1, j.Count("today"));
    }

    [Fact]
    public void Items_remember_where_they_came_from()
    {
        var (e, _, _, _) = Started();
        e.Bus.Send(new GrantItem("bg_space", "подарък"));
        Assert.Equal("подарък", e.Store.Get<JournalState>().OriginOf("bg_space")!.From);
        e.Bus.Send(new SetOrigin("bg_space", "от сандъчето на „Горски тайни“"));
        Assert.Equal("от сандъчето на „Горски тайни“", e.Store.Get<JournalState>().OriginOf("bg_space")!.From);
        // Само уточнение, ако вече има: не се презаписва.
        e.Bus.Send(new SetOrigin("bg_space", "друго", Force: false));
        Assert.Equal("от сандъчето на „Горски тайни“", e.Store.Get<JournalState>().OriginOf("bg_space")!.From);
    }

    [Fact]
    public void Talking_raises_the_threshold_and_silence_lowers_it()
    {
        var (e, clock, signals, talk) = Started();
        e.Bus.Send(new Think("a", 5));
        talk.Tick();
        Assert.Equal(new[] { "a" }, Said(signals));
        // Току-що е говорил: средна мисъл чака.
        clock.Now = clock.Now.AddMinutes(3);
        e.Bus.Send(new Think("b", 5));
        talk.Tick();
        Assert.Single(Said(signals));
        // След тишина – казва я.
        clock.Now = clock.Now.AddMinutes(40);
        talk.Tick();
        Assert.Equal(new[] { "a", "b" }, Said(signals));
    }

    [Fact]
    public void Busy_bear_says_only_the_most_important()
    {
        var (e, clock, signals, talk) = Started();
        e.Bus.Send(new ReportBearStatus(Asleep: false, Working: true, Outside: false, Activity: "work"));
        e.Bus.Send(new Think("chatter", 2));
        e.Bus.Send(new Think("care", 6));
        talk.Tick();
        Assert.Empty(Said(signals));
        e.Bus.Send(new Think("back.after.week", 10));
        talk.Tick();
        Assert.Equal(new[] { "back.after.week" }, Said(signals));
        // Свърши работата – казва и грижата.
        e.Bus.Send(new ReportBearStatus(false, false, false, "idle"));
        clock.Now = clock.Now.AddMinutes(45);
        talk.Tick();
        Assert.Contains("care", Said(signals));
    }

    [Fact]
    public void Unsaid_thoughts_fade_and_cooldown_blocks_repeats()
    {
        var (e, clock, signals, talk) = Started();
        e.Bus.Send(new Think("a", 9));
        talk.Tick();
        e.Bus.Send(new Think("low", 1, ExpireMinutes: 10));
        clock.Now = clock.Now.AddMinutes(11);
        talk.Tick();
        Assert.Contains(signals.OfType<ThoughtFaded>(), f => f.Thought.Key == "low");
        Assert.DoesNotContain("low", Said(signals));
        Assert.False(e.Bus.Send(new Think("a", 9, CooldownHours: 2)).Ok);
        clock.Now = clock.Now.AddHours(3);
        Assert.True(e.Bus.Send(new Think("a", 9, CooldownHours: 2)).Ok);
    }

    [Fact]
    public void Busy_day_says_more_than_quiet_day()
    {
        int Count(int ideasPerHour)
        {
            var (e, clock, signals, talk) = Started();
            for (int minute = 0; minute < 10 * 60; minute += 5)
            {
                if (minute % (60 / ideasPerHour) == 0) e.Bus.Send(new Think("idea" + minute, 4 + minute % 3, ExpireMinutes: 30));
                clock.Now = clock.Now.AddMinutes(5);
                talk.Tick();
            }
            return Said(signals).Count;
        }
        int quiet = Count(1), busy = Count(6);
        Assert.True(busy > quiet, $"пъстър {busy}, тих {quiet}");
        Assert.True(busy < 10 * 6, "не казва всичко подред");
    }

    [Fact]
    public void Attention_makes_him_chattier()
    {
        var (e, clock, signals, talk) = Started();
        e.Bus.Send(new Think("a", 6));
        talk.Tick();
        clock.Now = clock.Now.AddMinutes(5);
        e.Bus.Send(new Think("b", 5));
        talk.Tick();
        Assert.Single(Said(signals));
        e.Bus.Send(new UserAttention());
        e.Bus.Send(new UserAttention());
        talk.Tick();
        Assert.Equal(2, Said(signals).Count);
    }

    [Fact]
    public void Journal_and_talk_survive_save_and_load()
    {
        var (e, _, _, _) = Started();
        e.Bus.Send(new Remember("project.done", Facts.Of(("проект", "Горски тайни")), Importance: 8));
        e.Bus.Send(new GrantItem("bg_space", "подарък"));
        e.Bus.Send(new Think("care", 6));
        var saved = e.Store.Export();
        var (e2, _, _) = Kit.Engine();
        e2.Store.Import(System.Text.Json.Nodes.JsonNode.Parse(saved.ToJsonString())!.AsObject());
        e2.Modules.Add(new EconomyModule());
        e2.Modules.Add(new BearStatusModule());
        e2.Modules.Add(new JournalModule());
        e2.Modules.Add(new ThoughtsModule());
        e2.Start();
        var j = e2.Store.Get<JournalState>();
        Assert.Equal("Горски тайни", j.LastOf("project.done")!.Fact("проект"));
        Assert.Equal("подарък", j.OriginOf("bg_space")!.From);
        Assert.Single(e2.Store.Get<TalkState>().Pending);
    }

    [Fact]
    public void Tally_counts_forever_and_reports_round_numbers()
    {
        var (e, _, signals, _) = Started();
        for (int i = 0; i < 99; i++) e.Bus.Send(new Tally("pomodoro"));
        Assert.DoesNotContain(signals.OfType<TallyReached>(), t => t.Value == 100);
        e.Bus.Send(new Tally("pomodoro"));
        Assert.Contains(signals.OfType<TallyReached>(), t => t.Name == "pomodoro" && t.Value == 100);
        Assert.Equal(100, e.Store.Get<JournalState>().Counter("pomodoro"));
    }

    [Fact]
    public void Thoughts_without_lines_are_not_queued()
    {
        var (e, _, _, talk) = Started();
        talk.CanSay = k => k == "has.lines";
        Assert.False(e.Bus.Send(new Think("no.lines", 9)).Ok);
        Assert.True(e.Bus.Send(new Think("has.lines", 9)).Ok);
    }
}
