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
    ///   Next – „разгледай“: само с „Напред →“ (по менюто нищо не се натиска);
    ///   иначе – чака да направиш нещо (Until: „menu“, „page:food“, „newtask“, „taskadded“…) – и може да се
    ///   цъкне САМО огражданото (Allow: „target“), цялото прозорче (Allow: „window“) или да се местят задачи (Allow: „drag“).
    /// Choices – избор (само първата стъпка); Last – последната; Place – къде е балончето („right“).
    /// </summary>
    private sealed record TutorialStep(string Text, string Target, string? Until = null, bool Next = false, string[]? Choices = null,
        string? Button = null, bool Last = false, string Allow = "target", string? Place = null);

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
        new("Цъкни „Набери“ на Боровинките.", "berries", Until: "cook:berries"),
        new("Като сготвиш нещо, трябва да ми го донесеш, за да го хапна. Да знаеш, че повечето неща се готвят за време.", "none", Until: "ate:berries", Allow: "none"),
        new("Ще отключваме нови рецепти, като събираме странички за рецепти. Не го мисли за сега, с времето ще разбереш.", "foodinfo", Next: true),
        new("Върни се към главното меню.", Nav + MenuWindow.PageBear, Until: "page:" + MenuWindow.PageBear),
        new("Цъкни върху Бележки.", Nav + MenuWindow.PageNotes, Until: "page:" + MenuWindow.PageNotes),
        new("Тук можеш да си записваш бележки. Разгледай после, има разни удобни функции.", "page", Next: true),
        new("Цъкни върху Работа.", Nav + "work", Until: "page:" + MenuWindow.PageToday),
        new("Тук си записваш задачите. Така е измислено, че да си следиш задачите. Разгледай различните менюта после. Хайде да добавим една задача заедно.", "addtask", Until: "newtask"),
        new("Тук имаш различни опции да опишеш задачата си.", "newtask", Next: true),
        new("Там, където пише „Днес“, е денят, в който тази задача ще бъде.", "newtask:day", Next: true),
        new("График е кога в този ден искаш да я свършиш. Това можеш и после да го добавиш.", "newtask:schedule", Next: true),
        new("Време е времето, което мислиш, че ще ти отнеме да я направиш.", "newtask:time", Next: true),
        new("Етикет е какъв тип работа ще е. Можеш да добавиш собствени етикети.", "newtask:tag", Next: true),
        new("Приоритет е колко ти е спешна тази задача.", "newtask:priority", Next: true),
        new("Сложност е колко сложна е задачата ти. Това определя колко лешници ще ти донесе.", "newtask:size", Next: true),
        new("Добави една задача.", "newtask", Until: "taskadded", Allow: "window", Place: "right"),
        new("{timeline}", "page", Next: true, Allow: "drag"),
        new("Цъкни върху Седмица.", Nav + MenuWindow.PageWeek, Until: "page:" + MenuWindow.PageWeek),
        new("Това тук ти е разбивка на дните и всички задачи, които имаш и си си написала. Можеш да си местиш задачите в различни дни лесно – хвани една и я пренеси.", "page", Next: true, Allow: "drag"),
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
    private bool _tutorialTaskScheduled;

    /// <summary>Обиколката тече.</summary>
    public bool TutorialRunning => _tutorialStep >= 0 && !_save.TutorialDone;

    /// <summary>Сегашната стъпка (или null).</summary>
    private TutorialStep? TutorialNow => TutorialRunning && _tutorialStep < TutorialSteps.Length ? TutorialSteps[_tutorialStep] : null;

    /// <summary>Стъпка „разгледай“: продължава само с „Напред“.</summary>
    public bool TutorialWaitsNext => TutorialNow?.Next == true;

    /// <summary>Обиколката: менюто не се затваря (след като веднъж е отворено).</summary>
    public bool TutorialLocksMenu => TutorialRunning && _tutorialStep >= 2;

    /// <summary>
    /// Обиколката: може ли да се цъкне тук (екранна точка в DIP) в менюто/прозорчето за задача.
    /// Десният бутон – никъде; иначе – само огражданото (или прозорчето, или местене на задачи).
    /// </summary>
    internal bool TutorialAllowsClick(Point screen, bool right, Window from)
    {
        if (TutorialNow is not { } st) return true;
        if (right || st.Next && st.Allow != "drag") return false;
        return st.Allow switch
        {
            "window" => from is NewTaskWindow,
            "drag" => from is MenuWindow && TutorialTarget("page") is Rect pg && pg.Contains(screen),
            "none" => false,
            _ => TutorialTarget(st.Target) is Rect r && r.Contains(screen),
        };
    }

    /// <summary>Обиколката: спира цъкането в прозорец (меню, нова задача), ако там не бива.</summary>
    internal void TutorialGate(Window w, System.Windows.Input.MouseButtonEventArgs e, bool right)
    {
        if (!TutorialRunning) return;
        var src = PresentationSource.FromVisual(w);
        if (src?.CompositionTarget == null) return;
        // Местене, размер и превъртане – винаги може (не са част от урока).
        if (!right && IsWindowChrome(e.OriginalSource as DependencyObject, w)) return;
        var dip = src.CompositionTarget.TransformFromDevice.Transform(w.PointToScreen(e.GetPosition(w)));
        if (!TutorialAllowsClick(dip, right, w)) e.Handled = true;
    }

    /// <summary>
    /// Цъкането е по „рамката“ на прозореца: дръжката за размер, плъзгачът на превъртането,
    /// дръжката на лентата вляво или празно място, което мести прозореца (курсор „местене“).
    /// Бутон или нещо за цъкане по пътя нагоре – не е рамка.
    /// </summary>
    private static bool IsWindowChrome(DependencyObject? d, Window w)
    {
        for (; d != null && d != w; d = d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is System.Windows.Controls.Primitives.ResizeGrip or System.Windows.Controls.Primitives.ScrollBar) return true;
            if (d is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.TextBox) return false;
            if (d is FrameworkElement fe && fe.Cursor is { } c)
            {
                if (c == System.Windows.Input.Cursors.Hand) return false;
                if (c == System.Windows.Input.Cursors.SizeAll || c == System.Windows.Input.Cursors.SizeWE || c == System.Windows.Input.Cursors.SizeNWSE) return true;
            }
        }
        return false;
    }

    /// <summary>Самопроверка/прозорчето за задача: задачата е добавена (с график ли е).</summary>
    internal void TutorialTaskAdded(bool scheduled)
    {
        _tutorialTaskScheduled = scheduled;
        TutorialEvent("taskadded");
    }

    /// <summary>Пуска обиколката от началото (или от дадена стъпка).</summary>
    public void StartTutorial(int step = 0)
    {
        if (step >= TutorialSteps.Length)
        {
            FinishTutorial();
            React("happy");
            return;
        }
        if (step == 0)
        {
            _save.TutorialDone = false;
            _save.MenuPage = MenuWindow.PageBear;   // десният бутон отваря „Мечо“
        }
        if (OnWalk) ReturnFromWalk(called: true);
        if (IsAsleep) WakeUp();
        // Отначало: менюто е затворено, за да се започне от „цъкни ме“.
        if (step == 0 && MenuIsOpen) { _tutorialStep = -1; _menu!.Hide(); }
        int prev = _tutorialStep;
        _tutorialStep = step;
        var s = TutorialSteps[step];
        // Боровинките: тук трябва да може да се наберат веднага.
        if (s.Until == "cook:berries") _save.BerriesReadyAt = DateTime.MinValue;
        // Менюто – отзад, докато носиш боровинките; после – пак отпред.
        if (_menu != null) _menu.Topmost = s.Until != "ate:berries";
        if (_tutorial == null)
        {
            _tutorial = new TutorialOverlay();
            _tutorial.Stopped += () => FinishTutorial();
        }
        string text = s.Text.Replace("{име}", _save.OwnerName).Replace("{timeline}", _tutorialTaskScheduled
            ? "Браво! Можеш да движиш задачата по часовете в графика – хвани я и я пренеси."
            : "Браво! Можеш да сложиш задачата в графика, като я хванеш и пренесеш, а след това можеш да я движиш в графика по часове.");
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
            if (step > 1) buttons.Add(("← Назад", () => TutorialBack(), false));
            buttons.Add(("Стига", null, false));
            if (s.Next) buttons.Add((s.Button ?? "Напред →", () => TutorialEvent("next"), true));
        }
        _tutorial.Show(step == 0 || s.Last ? text : text + $"  ({step}/{TutorialSteps.Length - 2})", () => TutorialTarget(target), buttons, s.Place);
        // Менюто – цялото на екрана, а нещото, за което се говори – превъртяно да се вижда.
        // Два пъти: веднага и след като новата страница се нарисува.
        if (MenuIsOpen && !target.StartsWith("newtask") && target is not ("bear" or "none"))
        {
            _menu!.TutorialReveal(target);
            Later(0.4, () => { if (_tutorialStep == step && MenuIsOpen) _menu!.TutorialReveal(target); });
        }
        if (step > 0 && prev != step) Emote(s.Last ? "heart" : "idea", 2);
    }

    /// <summary>„← Назад“: предишната стъпка (стъпките с действие – до последната „разгледай“ преди тях).</summary>
    private void TutorialBack()
    {
        int k = _tutorialStep - 1;
        // Назад от „донеси боровинките“ / „добави задача“ няма смисъл да се повтаря действието.
        while (k > 1 && TutorialSteps[k].Until is "ate:berries" or "cook:berries" or "taskadded" or "newtask") k--;
        if (k == 1 && MenuIsOpen) k = 2;   // менюто вече е отворено
        StartTutorial(Math.Max(1, k));
    }

    /// <summary>Направи нещо (отвори менюто, таб, вдигна го…): ако стъпката чака точно него – напред.</summary>
    internal void TutorialEvent(string what)
    {
        if (_tutorialStep < 0 || _save.TutorialDone || _tutorialStep >= TutorialSteps.Length) return;
        var st = TutorialSteps[_tutorialStep];
        if (what == "next" ? !st.Next : st.Until != what) return;
        int next = _tutorialStep + 1;
        // Малко по-късно: менюто/табът първо да се нарисува, после квадратчето отива там.
        // В самопроверката – почти веднага (бавната машина иначе не смогва за проверката).
        Later(SelfTest.Running ? 0.05 : 0.35, () =>
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
        if (_menu != null) _menu.Topmost = true;
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
