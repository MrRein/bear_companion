using System;
using System.Windows;

namespace Mecho;

// Обиколка „Как се живее с Мечо?“: мечокът показва нещата едно по едно. Около това, за
// което става дума, свети квадратче (TutorialOverlay), а до него е балончето с текста и
// само „Добре, стига“. Напред се минава, като направиш каквото пише (цъкнеш мечока с десен
// бутон, отвориш таба, посочиш лентите…) – TutorialEvent. Пуска се сам веднъж (при първото
// пускане) и после от „Още“ → „Как се живее с Мечо?“.
public sealed partial class PetWindow
{
    /// <summary>Стъпка: текст, кое се огражда, и какво трябва да направиш, за да продължи (null – последната).</summary>
    private sealed record TutorialStep(string Text, string Target, string? Until);

    private static readonly TutorialStep[] TutorialSteps =
    {
        new("Здравей, {име}! Аз съм Мечо – ще ти покажа как живеем заедно. Цъкни ме с десен бутон веднъж – ще се отвори менюто ми.",
            "bear", "menu"),
        new("Това е менюто. Тук са ситостта и енергията ми – посочи ги с мишката и ще ти кажат как съм.",
            "bars", "bars"),
        new("Тук ме храниш, приспиваш и пращаш на разходка. Вечер ме слагай да спя – сам не лягам. Сега отвори „Работа“.",
            "tab:" + MenuWindow.PomodoroTab, "tab:" + MenuWindow.PomodoroTab),
        new("В „Работа“ пускаш мечо-доро или таймер и работим заедно – всеки 10 минути работа носят лешник, а свършените задачи – още. Отвори „Храна“.",
            "tab:" + MenuWindow.FoodTab, "tab:" + MenuWindow.FoodTab),
        new("С лешниците готвим тук. Горе се вижда колко страници от готварската книга остават до нова рецепта. Отвори „Магазин“.",
            "tab:" + MenuWindow.ShopTab, "tab:" + MenuWindow.ShopTab),
        new("В „Магазин“ събираме за уреди и мебели, а офертата на деня има дрехи, тапети и цветове за менюто. Сега затвори менюто – с ✕ или като цъкнеш встрани.",
            "menu", "menuclosed"),
        new("Можеш да ме вдигнеш с мишката и да ме пуснеш където искаш. Опитай!",
            "bear", "dropped"),
        new("Браво! Иконката ми е до часовника долу вдясно – с нея ме викаш, когато ме няма. Това е! Ако забравиш нещо, в „Още“ има „Как се живее с Мечо?“.",
            "none", null),
    };

    private TutorialOverlay? _tutorial;
    private int _tutorialStep = -1;

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
        string text = s.Text.Replace("{име}", _save.OwnerName) + $"  ({step + 1}/{TutorialSteps.Length})";
        string target = s.Target;
        _tutorial.Show(text, () => TutorialTarget(target), s.Until == null ? "Готово" : "Добре, стига");
        if (step > 0) Emote(s.Until == null ? "heart" : "idea", 2);
    }

    /// <summary>Направи нещо (отвори менюто, таб, вдигна го…): ако стъпката чака точно него – напред.</summary>
    internal void TutorialEvent(string what)
    {
        if (_tutorialStep < 0 || _save.TutorialDone || _tutorialStep >= TutorialSteps.Length) return;
        if (TutorialSteps[_tutorialStep].Until != what) return;
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
