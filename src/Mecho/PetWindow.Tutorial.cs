using System;

namespace Mecho;

// Обиколка (tutorial): мечокът обяснява стъпка по стъпка как се живее с него.
// Пуска се сам веднъж (при първото пускане след тази версия) и после от
// „Още“ → „Как се играе?“. Всяка стъпка е въпрос в чата: „Напред“, „Покажи ми“
// (отваря съответния таб) или „Стига, разбрах“.
public sealed partial class PetWindow
{
    private sealed record TutorialStep(string Text, int? Tab = null);

    private static readonly TutorialStep[] TutorialSteps =
    {
        new("Здравей, {име}! Аз съм Мечо. Да ти покажа ли набързо как живеем заедно?"),
        new("Цъкни ме веднъж и ще се отвори менюто. В „Мечо“ ме храниш, приспиваш, пращаш на разходка и слагаш печицата, лампата или мебелите („Неща на Мечо“).", MenuWindow.BearTab),
        new("Имам две ленти: ситост и енергия. Посочи ги с мишката и ще ти кажат как съм. Вечер ме слагай да спя – сам не лягам.", MenuWindow.BearTab),
        new("В „Работа“ пускаш мечо-доро или таймер и работим заедно. Всеки 10 минути работа носят лешник, а свършените задачи – още.", MenuWindow.PomodoroTab),
        new("С лешниците готвим в „Храна“. Раздел „Желание“ показва какво ми се яде днес – дадеш ли ми го, понякога ще ти върна услугата.", MenuWindow.FoodTab),
        new("Страниците от готварската книга ме учат на нови рецепти. Събираш ги от работа, задачи и подаръци – отгоре в „Храна“ се вижда колко остават.", MenuWindow.FoodTab),
        new("В „Магазин“ събираме за нови уреди и мебели (по 2–3 на ден), а офертата на деня има дрехи, тапети и цветове за менюто – те отиват в гардероба.", MenuWindow.ShopTab),
        new("Можеш да ме вдигнеш с мишката и да ме пуснеш където искаш. Храната я носиш до мен или я хвърляш по мен – хващам я!"),
        new("Иконката ми е до часовника: с ляв клик ме скриваш или викаш, а с десен има меню – „Отвори меню“ и „Скрий всичко“ (скрива рисунките)."),
        new("Това е! Ако забравиш нещо, в „Още“ има „Как се играе?“. Хайде да правим нещо заедно!"),
    };

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
        var s = TutorialSteps[step];
        string text = s.Text.Replace("{име}", _save.OwnerName) + $"  ({step + 1}/{TutorialSteps.Length})";
        var options = new System.Collections.Generic.List<ChatOption>();
        bool last = step == TutorialSteps.Length - 1;
        options.Add(new ChatOption(step == 0 ? "Да, покажи ми" : last ? "Готово" : "Напред", p => p.StartTutorial(step + 1)));
        if (s.Tab is int tab)
            options.Add(new ChatOption("Отвори го", p =>
            {
                p.OpenMenu(tab);
                p.ResumeTutorialAfterMenu(step + 1);
            }));
        if (!last) options.Add(new ChatOption(step == 0 ? "Не сега" : "Стига, разбрах", p => p.FinishTutorial()));
        Ask(new ChatQuestion(text, options.ToArray()));
        if (step > 0) Emote(step == TutorialSteps.Length - 1 ? "heart" : "idea", 2);
    }

    /// <summary>Като Тут затвори менюто (което ѝ показа), обиколката продължава.</summary>
    private void ResumeTutorialAfterMenu(int step)
    {
        Later(2, () =>
        {
            if (_save.TutorialDone) return;
            if (MenuIsOpen) ResumeTutorialAfterMenu(step);
            else StartTutorial(step);
        });
    }

    private void FinishTutorial()
    {
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
