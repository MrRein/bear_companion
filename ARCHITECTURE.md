# Архитектура на Мечо: как е направен и накъде да върви

Документът е за програмистите. Поведението (какво прави мечокът) е описано в
`BEHAVIOR.md`. Тук е **как** е построен, кои правила да спазва новият код и кои части
предстои да се преправят, преди програмата да излезе публично.

---

## 0. Ядрото (Mecho.Core) – фаза 1 е готова

`src/Mecho.Core` е отделна библиотека без WPF. Тестовете ѝ са в `tests/Mecho.Core.Tests` и CI ги пуска преди всяка версия.

| Част | Файл | За какво |
|---|---|---|
| `SignalBus` | `Signals/SignalBus.cs` | Сигнали (`ISignal` – станало е) към много слушатели. Команди (`IGameCommand` – искане) към точно един обработчик. Опашка без рекурсия, грешка в слушател не спира другите, безкраен цикъл се прекъсва, всичко е на една нишка. |
| Каталог сигнали | `Signals/GameSignals.cs` | Всички сигнали на играта (`NeedsChanged`, `ItemAcquired`, `DayStarted`, `GameEventStarted`…) |
| `GameStore` | `State/GameStore.cs` | Единственият източник на данните. Слоеве (`IStateSlice` – неизменими records), всяка промяна пуска `StateChanged<T>`. Има запис и четене с версии и миграции. Слой от по-нова версия не се презаписва. |
| `IGameModule`, `GameModule` | `Modules/IGameModule.cs` | Стандартът за механика: `Register` → `Start` → `Update` → `Shutdown`, с помощници `On`, `Emit`, `Handle`, `Send`, `State`, `Every`, `After` |
| `ModuleHost` | `Modules/ModuleHost.cs` | Пуска модулите по зависимостите. Липсваща или кръгова зависимост → само този модул не тръгва. 5 грешки за 60 с → модулът се спира и почиства. |
| `Scheduler` | `Time/Scheduler.cs` | „След…“ и „на всеки…“ с име и собственик (заменя полетата `_nextXxx`) |
| `IClock`, `IRandom` | `Time/Clock.cs` | Време и случайност, които се подменят в тестовете (и при „симулирай дата“) |
| `ModifierStack`, `Mods` | `Modifiers/ModifierStack.cs` | Ефектите на събитията (болен ден, Коледа…): множители, флагове, набори. Модулите питат модификатора, а не „болен ли е“. |
| `GameEngine` | `GameEngine.cs` | Сглобката. Пуска `DayStarted` при нов ден. |

В приложението:
* `src/Mecho/Engine/CoreSetup.cs` е **единственото** място, където се изброяват модулите.
* `LegacyBridgeModule` е временен мост: гледа стария код и пуска `NeedsChanged`, `HazelnutsChanged` и `SleepChanged`, докато механиките не станат модули.
* Състоянието на ядрото се записва в `save.json` → `Core`.
* В Developer има група „Ядро“: модули, последни сигнали, модификатори, слоеве, лог.

### Как се добавя нова механика (модул)
```csharp
public sealed record PajamaDayStarted(DateTime Day) : ISignal;              // 1. сигнал (ако трябва)
public sealed record PajamaDayState(bool Active, string Day) : IStateSlice; // 2. слой (ако пази данни)

public sealed class PajamaDayModule : GameModule                            // 3. модулът
{
    public override string Id => "event.pajama_day";
    protected override void OnRegister()
    {
        Store.Register(this, "event.pajama_day", new PajamaDayState(false, ""));
        On<DayStarted>(d =>
        {
            if (d.Day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) || !Random.Chance(0.1)) return;
            Store.Set(new PajamaDayState(true, Clock.DayKey()));
            Modifiers.Add(Id, Mods.WantedOutfit, "pajama");
            Emit(new GameEventStarted(Id));
        });
    }
}
// 4. в CoreSetup.Build:   engine.Modules.Add(new PajamaDayModule());
// 5. тест в tests/Mecho.Core.Tests (ManualClock + SystemRandom(зърно)) и стъпка в SelfTest.
```
Правила:
* Модулите **не** се реферират един друг – само шина и хранилище.
* Всеки слой има един собственик.
* Изгледът (прозорците) не е модул: чете хранилището, слуша сигналите и праща команди.

## 0.1 Фаза 3: свързване на системите (по вълни)

Свързването става на вълни. След всяка вълна CI проверява самопроверката и тестовете и чак тогава почва следващата. Старият код продължава да работи през **фасадата** на `SaveData`: свойствата ѝ четат от `GameStore` и пращат команди. Така данните са на едно място още от първата вълна, без целият стар код да се пренаписва наведнъж.

| Вълна | Система | Модул (ядро) | Слой | Команди → сигнали | Състояние |
|---|---|---|---|---|---|
| 1 | Лешници + притежавано | `EconomyModule` | `WalletState`, `InventoryState` | `EarnNuts`, `SpendNuts`, `Purchase`, `GrantItem`, `RevokeItem`, `SetInventory` → `HazelnutsChanged`, `ItemAcquired`, `ItemRevoked` | ✅ |
| 1 | Предмети на земята | `PlacementModule` (зависи от economy) | `PlacementState` | `PlaceItem`, `RemoveItem`, `ReleaseByBear`, `SwitchItem`, `MoveItem`, `SetSupply` → `ItemPlaced`, `ItemRemoved`, `ItemSwitched`, `ItemMoved` | ✅ |
| 2 | Нужди (глад, енергия) + какво прави мечокът | `NeedsModule` (гладът спада тук, × `Mods.HungerRate`), `BearStatusModule` | `NeedsState`, `BearStatusState` | `AdjustNeeds`, `SetNeeds`, `ReportBearStatus` → `NeedsChanged`, `SleepChanged`, `ActivityChanged` | ✅ (енергията още се смята в `PetWindow.Energy`, но пише в ядрото и ползва `Mods.EnergyDrain`) |
| 3 | Храна и готвене, желания | `KitchenModule` | `CookingState`, `WishesState` | `StartCook`, `NextCookStage`, `MoveCooker`, `EndCook`, `SetWishes`, `AddWish`, `FulfillWish` → `CookingStarted`, `CookingEnded`, `WishAdded`, `WishFulfilled`; приложението пуска `FoodEaten` | ✅ (каталогът с ястия и анимациите остават в приложението) |
| 4а | Работа: мечо-доро, таймер, плащане | `WorkModule` (зависи от economy; САМ засича края всяка секунда) | `PomodoroState`, `WorkTimerState`, `WorkBankState` | `StartPomodoro`, `StopPomodoro`, `SetPomodoroMode`, `SetPomodoroSeries`, `StartWorkTimer`, `StopWorkTimer`, `CheckWork`, `PayWork` → `PomodoroStarted`, `PomodoroFocusDone`, `PomodoroBreakDone`, `PomodoroStopped`, `WorkTimerFinished`, `WorkTimerStopped`, `WorkPaid` | ✅ (мечокът само слуша сигналите: `PetWindow.WireWork`) |
| 4б | Задачи | `TasksModule` (зависи от economy) | `TasksState` (`TaskEntry` = `TaskItem` в приложението) | `AddTask`, `SetTaskDone`, `DeleteTask`, `SetFocusTask`, `ArchiveDoneTasks` → `TaskAdded`, `TaskCompleted`, `TaskReopened`, `TaskDeleted` | ✅ (планът за деня „Ще се работи ли днес?“ остава в приложението) |
| 5 | Гардероб, скинове, пижама, меню, фон | `WardrobeModule` | `LooksState` | `Wear`, `TakeOff`, `TakeOffAll`, `UseSkin`, `SetLook` → `OutfitChanged`, `SkinChanged` | ✅ (магазинът/офертата остават в `Shop.cs`; купуването вече е `Purchase` в economy) |
| 6 | Подаръкът на деня, растението | `GiftsModule` (шанс × `Mods.GiftChance`), `GardenModule` (грижа: 8 мин = фаза, до 2 на ден; само – на 5 дни) | `GiftState`, `PlantState` | `PlanGiftToday`, `GiftPresented`, `GiftOpened`, `CareForPlant`, `GrowPlant`, `CheckPlantGrowth` → `GiftPlanned`, `PlantGrew` | ✅ (хранилката е в `PlacementModule` от вълна 1) |
| 7 | Разговори, въпросник, спомени | – | – | – | ⏸ остават в приложението (паметта на деня `DayRecord` е вързана с почти всичко; следва, когато се пипа) |
| 8 | Събития: Коледа, пижамен ден, болен ден, пеперуда, дъга | `EventDirector` (събитията са ДАННИ: `GameEvents.All`) | `EventsState` | `StartGameEvent`, `EndGameEvent` → `GameEventStarted`, `GameEventEnded`; докато трае – модификатори под `event.<id>` | ✅ (мечокът: `PetWindow.Events.cs`) |
| 9 | Изгледи: менюто по табове, мечокът, уредите | – (изглед) | – | само четат и слушат | |

**Свързано досега (вълна 1):**
* Менюто слуша `StateChanged<WalletState|InventoryState|PlacementState>` и се пречертава само. Абонаментът се маха при затваряне.
* Танцът при радиото праща `PlaceItem(radio, Bear)` и `SwitchItem(…, ByBear)`; краят на танца праща `ReleaseByBear`.
* Продаден предмет слиза от земята сам (`ItemRevoked`).
* Сложеното от мечока не оцелява след рестарт (`PlacementModule.Start`).
* Dev Mode пази и връща слоевете през `Store.Restore`.
* Стари записи се местят в ядрото при първото пускане (`SaveData.Bind`).
* Вълна 3: готвенето (какво, докога, кой уред, къде) и желанията за деня са само в ядрото; `SaveData.CookFood`/`WishList` са само за четене, пише се с команди. Желано ястие се маха с `FulfillWish` и пуска `WishFulfilled`; всяко изядено пуска `FoodEaten`.
* Вълна 4а: мечо-дорото и таймерът са в ядрото. `WorkModule` сам засича края (и поредицата работа → почивка → работа), брои мечо-дората за деня и плаща по 1 лешник на 10 минути. Мечокът (`PetWindow.WireWork`) само слуша `PomodoroFocusDone`, `PomodoroBreakDone` и `WorkTimerFinished` и реагира.
* Вълна 4б: задачите са неизменими записи в ядрото. Наградата (1/3/5 лешника), броячът и връщането при отмяна са в `TasksModule`. Свършените от минали дни отиват в историята сами в полунощ.

## 0.2 Събития и разширяване – 4 стъпки

**Ново събитие** (пр. „Ден на палачинките“):
1. Запис в `Mecho.Core.GameEvents.All`: `Id`, `Name`, кога (`On = d => …`, `Fixed` или `DailyChance`/`HourlyChance`), `CooldownDays` и какво сменя (`Apply = (m, s) => { m.Set(s, Mods.HungerRate, 1.3); m.Add(s, Mods.ShopExtras, "pancake_mix"); }`).
2. Реплики в `assets/repliki.txt`: `=== event.<id>.start`, `.end`, `.chatter` (по желание).
3. Само ако има нещо ново за показване: `case` в `PetWindow.Events.cs` (`EventStarted`). Модулите не се пипат – те четат модификаторите.
4. Тест в `tests/Mecho.Core.Tests/EventsTests.cs` и при нужда стъпка в `SelfTest.cs`. Developer → „Събития“ пуска всяко на ръка.

**Нов модул** (нова механика): клас `XModule : GameModule` в `Mecho.Core/Systems` (слой с `Store.Register`, команди с `Handle`, сигнали с `Emit`) → ред в `Engine/CoreSetup.cs` → тест в `tests/Mecho.Core.Tests` → мечокът слуша сигналите му (`bus.Subscribe<…>(this, …)`), менюто – `StateChanged<XState>`.

**Болен ден** е пример за всичко: `Mods.EnergyDrain` (енергията пада по-бързо), `Mods.HungerRate`, `Mods.BlockedHobbies` (без батут), `Mods.ShopExtras` („medicine“ – лекарството се показва в „Храна“ само докато трае, `Kitchen.EventTag`), а изпитото лекарство праща `EndGameEvent(sick_day, "лекарство")`.

Самопроверката пуска режисьора с `automatic: false` – нищо не почва само насред проверка.

## 1. Сегашният строеж накратко

| Част | Къде | Бележка |
|---|---|---|
| Мечокът (поведение, състояния, кътчета) | `PetWindow*.cs` (един `partial class` в ~40 файла) | Най-голямата техническа дългова част – виж §3.1 |
| Записът | `SaveData.cs` → `%AppData%\Mecho\save.json` | Версия на формата, резервни копия – виж §2 |
| Текстовете | `assets/repliki.txt` (R), `razgovori.txt` (Talks), `anketi.txt` (Survey), `Lines.cs` | Нов код: само `R.Text(...)` – виж §2 |
| Менюто | `MenuWindow.cs` (UI в код) | Голям файл; да се раздели по табове |
| Предмети на земята | `CookerWindow` (уред, печица, лампа, радио, хранилка), `OverlayWindow` (саксия, балонче, пеперуда, птичка, нотки), `GiftWindow`, `FoodWindow` | Правилата са в `PetWindow.Placeables.cs` и `PetWindow.Anchors.cs` |
| Самопроверка | `SelfTest.cs` (`Mecho.exe --selftest`, пуска се в CI) | Пише в отделна временна папка |
| Обновяване | `Updater.cs` + `.github/workflows/build.yml` | Проверява SHA-256 на архива |

---

## 2. Правила за нов код (вече ги има – ползвайте ги)

1. **Текст с полета:** `R.Text("ключ", owner, ("ястие", име), ("уред", "тенджерата"))`.
   * `{Ястие}` с главна буква дава стойността с главна буква.
   * Не пишете нов `.Replace("{…}")` на място.
   * `{име}`, `{брой}` и `{храна}` се превръщат при зареждане в `{0}`, `{1}` и `{2}`. `R.Put` и `R.Text` знаят това.
2. **„Колко пъти днес“:** `_save.CountToday("име")` и `_save.TodayCount("име")`. Не добавяйте нова двойка полета `XxxDay` + `XxxToday`. Старите двойки ще се преместят тук постепенно.
3. **Денят като ключ:** `SaveData.DayKey(дата)`, вместо `ToString("yyyy-MM-dd")`.
4. **Ново поле в записа:**
   * Достатъчно е свойство с подразбираща се стойност.
   * Преименуване, смяна на типа или преместване изискват вдигане на `SaveData.CurrentSchema` и стъпка в `SaveData.Migrate`.
5. **Кратка реакция** (радост, танц) – само с `React(anim)`, никога с `Play(...)` направо.
   * Ако мечокът е тръгнал по задача (`OnErrand`), `Play` би изтрил следващата стъпка и той би заседнал. `React` тогава показва само емоция.
6. **Занимание с предмет:**
   * Кътче, вързано за предмет: `_sceneNear = XxxAnchor` след `StartScene`.
   * Кратко действие с предмет: `PlayNear(anchor, anim, …)`.
   * Тогава анимацията върви само до предмета. Ако мечокът е вдигнат – виси с `drag`. Ако е далече или предметът е преместен – отива при него и продължава. Ако предметът изчезне – заниманието свършва.
   * Котвите са в `PetWindow.Anchors.cs`.
7. **Предмет, сложен от мечока** (радиото за танц, саксията от ъгъла):
   * Маха се, щом свърши заниманието.
   * Светва в „Сложи“, а цъкането там спира заниманието.
   * Сложеното от Тут остава.
   * От всеки вид има само по един – виж `PetWindow.Placeables.cs`.
8. **Междинни състояния** (`_walkingOut`, `_peeking` и т.н.): ако добавяте ново, добавете проверка в `CheckIntegrity` (`PetWindow.Integrity.cs`). Тя трябва да го доведе до края, ако веригата се прекъсне.
9. **Самопроверка:** всяка нова механика – стъпка в `SelfTest.cs`. CI я пуска при всяко качване. Ако не мине, версия не излиза.

---

## 3. Какво да се преправи (по важност)

### 3.1 Заниманията като обекти (Activity) – **най-важното, L**

**Сега:** поведението е верига от `WalkTo(x, после)` и `Play(anim, after:)`. Следващата стъпка е в едно поле (`_afterAction`), което всеки `Play`/`WalkTo`/`SetState` може да изтрие. Полу-състоянията са разпилени във флагове: `_giftRun`, `_gardenPhase`, `_plantPushing`, `_walkingOut`, `_peeking`, `_closet`, `_trip`, `_pendingCook`… Затова има „часовници пазачи“ (`CheckIntegrity`, срокът на подаръка, срокът на `_trip`).

**Целта:**
```
interface IActivity {
    int Priority;                     // команда от Тут > нужди > поръчки (подарък, колет) > режим > свободно
    void Enter(Body b); void Update(double dt); void Exit(ExitReason r);
    bool CanBeInterruptedBy(IActivity other);
    void Suspend(); void Resume();    // вдигнат, дойде храна…
}
```
* `ActivityRunner` пази стек от занимания.
* `Body` (позиция, анимация, посока) дава `WalkTo`/`Play` като помощници без „после“.
* Всяко занимание е малка машина на състоянията в своя `Update`. Пример – подаръкът: Leaving → Away → Returning → Holding.
* `Exit` винаги се вика и чисти своето (флаговете отпадат).

**Пътят, без да се пренаписва всичко наведнъж:**
1. Runner и Body; сегашният код става едно „наследено“ занимание.
2. Първи се преместват разходка, подарък, надничане и растение.
3. После колет, гардероб и поръчките при готвене.
4. Махат се пазачите в `CheckIntegrity`, щом станат излишни.

### 3.2 Предмети на земята – един модел, M

Печица, лампа, радио и хранилка повтарят по ~30 реда: създаване, `Dropped`, `Persist`, `SetAway`, `Step`. Собствеността е в различни полета: `RadioPlaced`/`RadioAuto`, `HeaterOn`, `LampPlaced`/`LampOff`, `PlantPlaced`.

**Целта:** `PlacedItem { Kind, Owner (Тут/Мечо/никой), On, X, Art, ClickMenu }` и `PlacedItemManager`. Той гарантира едно от вид, мести, хвърля, крие с мечока и вдига отгоре, а в записа е `Dictionary<string, PlacedItemState>`. „Сложено от мечока“ става `Owner = Мечо`, а заниманието го освобождава в `Exit`.

### 3.3 Записът по раздели, M

`SaveData` е ~170 свойства едно до друго. Да се групира в `Care`, `Pomodoro`, `Timer`, `Cooking`, `Gift`, `Placed`, `Weather`, `Survey`, `Memory`, `Settings`, `Shop` – с миграция (схема 2). `Owned` (списък с префикси `bg_`, `skin_`…) да стане по категории.

### 3.4 Текстовете – един API, M

* `Lines.cs` и `Scenes` четат текста в `static readonly` и затова не се презареждат.
* Тестетата са по масив, а не по име на раздел.
* Има четири парсера (R, Talks, Survey, FoodTalk) с различни полета.

**Целта:** всичко през `R.Text` и проверка при зареждане кои полета ползва всеки раздел, със самопроверка, че няма непопълнени.

### 3.5 Време и случайност – M

~30 полета `_nextXxx` в паметта и ~180 `DateTime.Now`.

**Целта:**
* `IClock` и `IRandom`, за да могат да се подменят в тестовете.
* `Scheduler` с именувани задачи (`Every("chatter", 40..70 мин, условие)`).
* `Later` със „собственик“, за да отпада с заниманието си.

### 3.6 Обновяване – M (преди публичното пускане)

**Сега:** SHA-256, отделна папка за всяка версия и копиране с PowerShell.

**Целта:** Velopack (или Squirrel) за атомарна смяна, без остатъци от стари файлове, без `-ExecutionPolicy Bypass`, с подпис на exe.

### 3.7 Тестове извън приложението – M, постепенно

Чистата логика да се извади в класове без WPF, с xUnit тестове:
* грижите (глад и енергия);
* планът за подаръците;
* растежът на растението;
* студът;
* изборът на занимание;
* броячите;
* миграцията;
* текстовете.

Самопроверката в приложението остава кратък smoke test.

### 3.8 Дребни
* `MenuWindow.cs` (~2500 реда) – по един файл за таб.
* `CarryRect`/`ButterflyRect` сменят видимостта от функции за подреждане – да се раздели.
* Печицата, лампата, радиото и хранилката се движат с фиксирано `Step(0.033)` вместо с истинското dt.
* Времето взима мястото по IP без питане. За публично пускане – съгласие или избор на град.
* Пикселният шрифт е с некомерсиален лиценз – да се провери, ако програмата ще се продава.

---

## 4. Какво вече е направено (версия от 18.10.2026)

* **Самопроверката** пише във временна папка (`MechoSelfTest`), а не върху истинския запис.
* **Записът:**
  * има `SchemaVersion` и място за миграции;
  * пази резервни копия веднъж на час (`save.bak1.json`, `save.bak2.json`);
  * ако е повреден – зарежда копието;
  * грешките при запис се отбелязват.
* **`crash.log`:** еднаква грешка се записва веднъж на 10 мин; над 1 MB се отрязва.
* **Обновяването** проверява SHA-256 на `Mecho.zip`. CI публикува `Mecho.zip.sha256`.
* **Реакциите** (`React`) не прекъсват задачи. `CheckIntegrity` довършва прекъсната разходка или надничане.
* **Саксията** не „отвлича“ следващата разходка. Паузата (колет) чисти сложените от мечока неща.
* **Занимания с предмет:** котвите в `PetWindow.Anchors.cs`.
* **Нови API за бъдещия код:** `R.Text`, `CountToday` и `TodayCount`, `SaveData.DayKey`.
* **Скорост:** масивите с кътчета се пазят, а не се правят наново всеки кадър.
