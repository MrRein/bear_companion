using System;
using System.Collections.Generic;
using System.Windows;

namespace Mecho;

// Обиколка „Как се живее с Мечо?“: мечокът показва нещата едно по едно. Около това, за
// което става дума, свети квадратче (TutorialOverlay), а до него е балончето с текста и
// само „Добре, стига“. Напред се минава, като направиш каквото пише (цъкнеш мечока с десен
// бутон, отвориш таба, посочиш лентите…) – TutorialEvent. Пуска се сам веднъж (при първото
// пускане) и после от „Още“ → „Как се живее с Мечо?“.
public sealed partial class PetWindow
{
    /// <summary>
    /// Стъпка: текст, кое се огражда, и как се продължава:
    ///   Next – „разгледай“: продължава с „Напред →“ или с цъкане върху менюто (бутоните тогава не се натискат);
    ///   иначе – чака да направиш нещо (Until: „menu“, „page:food“, „newtask“, „taskadded“…).
    /// Choices – избор (само първата стъпка); Last – последната.
    /// </summary>
    private sealed record TutorialStep(string Text, string Target, string? Until = null, bool Next = false, string[]? Choices = null, string? Button = null, bool Last = false);

    private const string Nav = "nav:";

    private static readonly TutorialStep[] TutorialSteps =
    {
        new("Здравей, Тутник. Аз съм твоето персонално помощно Мечо. Дошъл съм да ти правя компания като работиш и да си работим заедно. Ще се опитам да ти помагам да се концентрираш и да си следиш задачите. Големият ти Мечок ме поръча и ето ме, той е виновен, че съм тук... хах. Е, ще трябва да ми даваш тук-там да тъпкам и да посънчвам, но ще се опитам да не прекалявам и да не съм супер досаден. Искаш ли да те преведа през различните неща, които можем да правим заедно?",
            "bear", Choices: new[] { "Да, хайде.", "Не мерси, ще го измисля сама." }),
        new("Добре, цъкни върху мен с десен бутон, за да отвориш менюто.", "bear", Until: "menu"),
        new("Това е менюто и ще правиш повечето неща от тук.", "menu", Next: true),
        new("Това са ми главните статистики, те показват колко съм гладен и уморен.", "bars", Next: true),
        new("Това са броя лешници, които сме събрали с теб. Те се ползват за повечето неща и се събират като си работим заедно.", "nuts", Next: true),
        new("Това ми е дневничето. В него си записвам как се чувствам, какво правя и какво ми се иска.", "diary", Next: true),
        new("Тук ще са проектите, които правим заедно и които сме направили.", "projects", Next: true),
        new("Това са всички действия, които можеш да правиш с мен. Няма да влизам в детайл – ако се чудиш за нещо, питай Големия Мечок.", "actions", Next: true),
        new("От тук ме храниш – отвори менюто.", "tile:food", Until: "page:" + MenuWindow.PageFood),
        new("Това е менюто с храна, която можеш да ми готвиш. Храните се купуват с лешници.", "page", Next: true),
        new("Ще отключваме нови рецепти, като събираме странички за рецепти. Не го мисли за сега, с времето ще разбереш.", "foodinfo", Next: true),
        new("Върни се към главното меню.", Nav + MenuWindow.PageBear, Until: "page:" + MenuWindow.PageBear),
        new("Цъкни върху Бележки.", Nav + MenuWindow.PageNotes, Until: "page:" + MenuWindow.PageNotes),
        new("Тук можеш да си записваш бележки. Разгледай после, има разни удобни функции.", "page", Next: true),
        new("Цъкни върху Работа.", Nav + "work", Until: "page:" + MenuWindow.PageToday),
        new("Тук си записваш задачите. Така е измислено, че да си следиш задачите. Разгледай различните менюта после. Хайде да добавим една задача заедно.", "addtask", Until: "newtask"),
        new("Тук имаш различни опции да опишеш задачата си.", "newtask", Next: true),
        new("Там, където пише „Днес“, е денят, в който тази задача ще бъде.", "newtask:day", Next: true),
        new("График е кога в този ден искаш да я свършиш. Това можеш и после да го добавиш.", "newtask:schedule", Next: true),
        new("Време е времето, което мислиш, че ще ти отнеме да я направиш. Етикет е какъв тип работа ще е. Можеш да добавиш собствени етикети.", "newtask:time", Next: true),
        new("Приоритет е колко ти е спешна тази задача.", "newtask:priority", Next: true),
        new("Сложност е колко сложна е задачата ти. Това определя колко лешници ще ти донесе.", "newtask:size", Next: true),
        new("Добави една задача.", "newtask", Until: "taskadded"),
        new("Браво. Това отстрани ти е графикът по часове на деня. Можеш да движиш задачите там.", "timeline", Next: true),
        new("Цъкни върху Седмица.", Nav + MenuWindow.PageWeek, Until: "page:" + MenuWindow.PageWeek),
        new("Това тук ти е разбивка на дните и всички задачи, които имаш и си си написала. Можеш да си местиш задачите в различни дни лесно.", "page", Next: true),
        new("Цъкни Фокус.", Nav + MenuWindow.PageFocus, Until: "page:" + MenuWindow.PageFocus),
        new("Това е доста важно. От тук можеш да си пускаш мечо-дорота и таймери за работа. Има различни конфигурации мечо-доро и поредност да се пускат по няколко пъти подред. Като си ги пускаш за работа, ни носят лешници.", "page", Next: true),
        new("Остана малко – цъкни Календара.", Nav + MenuWindow.PageCalendar, Until: "page:" + MenuWindow.PageCalendar),
        new("Тук можеш да следиш задачите си в различни варианти – знаеш как работи календар. Също можеш да си свържеш Google Calendar и да ти излизат тук нещата от него.", "page", Next: true),
        new("Цъкни Чакащи.", Nav + MenuWindow.PageBacklog, Until: "page:" + MenuWindow.PageBacklog),
        new("Това са задачи, които още не знаеш къде да разпределиш. Ако знаеш, че ще има нещо, което трябва да свършиш в бъдеще, но не знаеш кога, можеш да го сложиш тук.", "page", Next: true),
        new("Цъкни Преглед.", Nav + MenuWindow.PageReview, Until: "page:" + MenuWindow.PageReview),
        new("Тук ще ти излиза статистика за задачите за седмицата. Не го мисли за сега.", "page", Next: true),
        new("И финално – Още.", Nav + MenuWindow.PageMore, Until: "page:" + MenuWindow.PageMore),
        new("Тук можеш да нагласиш разни неща – колко съм голям, звуци и т.н.", "page", Next: true, Button: "Приключи турът"),
        new("Ами добре, това беше, дано не съм те отегчил. Да започваме с веселите неща, а?", "none", Last: true, Button: "Да!"),
    };

    private TutorialOverlay? _tutorial;
    private int _tutorialStep = -1;

    /// <summary>Обиколката тече.</summary>
    public bool TutorialRunning => _tutorialStep >= 0 && !_save.TutorialDone;

    /// <summary>Стъпка „разгледай“: цъкане върху менюто продължава (а бутоните там не се натискат).</summary>
    public bool TutorialWaitsNext => TutorialRunning && _tutorialStep < TutorialSteps.Length && TutorialSteps[_tutorialStep].Next;

    /// <summary>Пуска обиколката от началото (или от дадена стъпка).</summary>
    public void StartTutorial(int step = 0)
    {
        if (step >= TutorialSteps.Length)
        {
            FinishTutorial();
            React("happy");
            return;
        }
        if (step == 0) _save.TutorialDone = false;
        if (OnWalk) ReturnFromWalk(called: true);
        if (IsAsleep) WakeUp();
        // Отначало: менюто е затворено, за да се започне от „цъкни ме“.
        if (step == 0 && MenuIsOpen) _menu!.Hide();
        _tutorialStep = step;
        var s = TutorialSteps[step];
        if (_tutorial == null)
        {
            _tutorial = new TutorialOverlay();
            _tutorial.Stopped += () => FinishTutorial();
        }
        string text = s.Text.Replace("{име}", _save.OwnerName);
        string target = s.Target;
        var buttons = new List<(string, Action?, bool)>();
        if (s.Choices is { } ch)
        {
            buttons.Add((ch[0], () => StartTutorial(step + 1), true));
            buttons.Add((ch[1], null, false));
        }
        else if (s.Last) buttons.Add((s.Button ?? "Готово", () => { FinishTutorial(); React("happy"); }, true));
        else
        {
            buttons.Add(("Стига", null, false));
            if (s.Next) buttons.Add((s.Button ?? "Напред →", () => TutorialEvent("next"), true));
        }
        _tutorial.Show(step == 0 || s.Last ? text : text + $"  ({step}/{TutorialSteps.Length - 2})", () => TutorialTarget(target), buttons);
        if (step > 0) Emote(s.Last ? "heart" : "idea", 2);
    }

    /// <summary>Направи нещо (отвори менюто, таб, вдигна го…): ако стъпката чака точно него – напред.</summary>
    internal void TutorialEvent(string what)
    {
        if (_tutorialStep < 0 || _save.TutorialDone || _tutorialStep >= TutorialSteps.Length) return;
        var st = TutorialSteps[_tutorialStep];
        if (what == "next" ? !st.Next : st.Until != what) return;
        int next = _tutorialStep + 1;
        // Малко по-късно: менюто/табът първо да се нарисува, после квадратчето отива там.
        Later(0.35, () =>
        {
            if (_tutorialStep == next - 1 && !_save.TutorialDone) StartTutorial(next);
        });
    }

    /// <summary>Къде е нещото, за което говори стъпката (в DIP на екрана), или null.</summary>
    private Rect? TutorialTarget(string target)
    {
        if (target == "bear") return IsVisible ? _bearRect : null;
        if (target == "none") return null;
        if (target.StartsWith("newtask")) return NewTaskWindow.Current?.TutorialTarget(target);
        if (!MenuIsOpen) return target == "menu" ? null : IsVisible ? _bearRect : null;   // менюто е затворено: пак към мечока
        return _menu!.TutorialTarget(target);
    }

    private void FinishTutorial()
    {
        _tutorialStep = -1;
        _tutorial?.Close();
        _save.TutorialDone = true;
        Persist();
    }

    /// <summary>При пускане: ако обиколката още не е показвана – след малко я предлага.</summary>
    private void MaybeStartTutorial()
    {
        if (_save.TutorialDone || SelfTest.Running) return;
        Later(6, () =>
        {
            if (_save.TutorialDone || ChatIsOpen || MenuIsOpen || OnWalk) return;
            StartTutorial();
        });
    }
}
