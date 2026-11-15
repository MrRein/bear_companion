using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Етикетите на задачите: ключът се пази в задачата, името и цветът – в записа (SaveData.TaskTags).
/// Тут си ги пише сама и си избира цвета; първия път са шестте готови.
/// </summary>
internal static class TaskTags
{
    public sealed record Info(string Key, string Name, Color Color);

    /// <summary>Цветовете за избор (големи квадратчета, без рамки).</summary>
    public static readonly Color[] Palette =
    {
        Color.FromRgb(84, 132, 204), Color.FromRgb(58, 164, 164), Color.FromRgb(98, 168, 82), Color.FromRgb(170, 196, 72),
        Color.FromRgb(230, 192, 56), Color.FromRgb(222, 150, 54), Color.FromRgb(220, 90, 80), Color.FromRgb(214, 104, 146),
        Color.FromRgb(146, 108, 196), Color.FromRgb(70, 92, 150), Color.FromRgb(150, 104, 72), Color.FromRgb(128, 128, 136),
    };

    private static readonly (string Key, string Name, Color Color)[] Defaults =
    {
        ("work", "Работа", Color.FromRgb(84, 132, 204)),
        ("art", "Рисуване", Color.FromRgb(214, 104, 146)),
        ("study", "Учене", Color.FromRgb(146, 108, 196)),
        ("home", "Вкъщи", Color.FromRgb(222, 150, 54)),
        ("health", "Здраве", Color.FromRgb(98, 168, 82)),
        ("people", "Хора", Color.FromRgb(58, 164, 164)),
    };

    public const int MaxTags = 24, MaxName = 24;

    private static List<TaskTagDef> _defs = new();
    private static Info[]? _all;

    /// <summary>Вика се при пускане: етикетите от записа (първия път – готовите).</summary>
    public static void Use(SaveData save)
    {
        save.TaskTags ??= Defaults.Select(d => new TaskTagDef { Key = d.Key, Name = d.Name, Color = Hex(d.Color) }).ToList();
        _defs = save.TaskTags;
        Changed();
    }

    public static Info[] All => _all ??= _defs.Select(d => new Info(d.Key, d.Name, Parse(d.Color))).ToArray();

    public static Info? Find(string? key) => string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(t => t.Key == key);

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color Parse(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch (Exception) { return Palette[0]; }
    }

    /// <summary>Нов етикет (празно име или вече има такъв – null; с такова име – връща него).</summary>
    public static string? Add(string name, Color color)
    {
        name = name.Trim();
        if (name.Length > MaxName) name = name[..MaxName];
        if (name.Length == 0) return null;
        if (All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.CurrentCultureIgnoreCase)) is { } same) return same.Key;
        if (_defs.Count >= MaxTags) return null;
        string key;
        do key = "t" + Guid.NewGuid().ToString("N")[..6]; while (_defs.Any(d => d.Key == key));
        _defs.Add(new TaskTagDef { Key = key, Name = name, Color = Hex(color) });
        Changed();
        return key;
    }

    public static void Rename(string key, string name)
    {
        name = name.Trim();
        if (name.Length > MaxName) name = name[..MaxName];
        if (name.Length == 0 || _defs.FirstOrDefault(d => d.Key == key) is not { } d || d.Name == name) return;
        d.Name = name;
        Changed();
    }

    public static void Recolor(string key, Color color)
    {
        if (_defs.FirstOrDefault(d => d.Key == key) is not { } d) return;
        d.Color = Hex(color);
        Changed();
    }

    /// <summary>Маха етикета (задачите с него остават без етикет – виж PetWindow.DeleteTaskTag).</summary>
    public static void Remove(string key)
    {
        if (_defs.RemoveAll(d => d.Key == key) > 0) Changed();
    }

    /// <summary>Цвят, който още не е взет (за нов етикет).</summary>
    public static Color NextColor() =>
        Palette.FirstOrDefault(c => All.All(t => t.Color != c), Palette[_defs.Count % Palette.Length]);

    /// <summary>Сменя се при всяка промяна (за да се пречертаят филтрите и т.н.).</summary>
    public static int Version { get; private set; }

    private static void Changed()
    {
        Version++;
        _all = null;
        Brushes.Clear();
    }

    private static readonly Dictionary<string, Brush> Brushes = new();

    public static Brush BrushOf(string key, byte alpha = 255)
    {
        string id = key + "/" + alpha;
        if (Brushes.TryGetValue(id, out var b)) return b;
        var c = Find(key)?.Color ?? Colors.Transparent;
        var brush = new SolidColorBrush(Color.FromArgb(c.A == 0 ? (byte)0 : alpha, c.R, c.G, c.B));
        brush.Freeze();
        return Brushes[id] = brush;
    }

    /// <summary>Квадратче в цвета на етикета – само цвят, без рамка.</summary>
    public static Border Swatch(string key) => ColorSquare(Find(key)?.Color ?? Colors.Transparent);

    public static Border ColorSquare(Color c, double size = 4) => new()
    {
        Width = size * P,
        Height = size * P,
        Background = new SolidColorBrush(c),
        CornerRadius = new CornerRadius(2),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 6, 0),
    };
}

/// <summary>Как се показва задача (за картичката), на едно място за менюто, таблото и седмицата.</summary>
public sealed class TaskCardOptions
{
    /// <summary>Цъкване по картичката (не по отметката и копчетата) – например избор в седмицата.</summary>
    public Action<TaskItem>? Select { get; init; }
    public bool Selected { get; init; }
    /// <summary>Описанието под името (съкратено до толкова знака; 0 – без).</summary>
    public int Description { get; init; } = 70;
    /// <summary>Копчета вдясно (например 📍 ✕ в таблото).</summary>
    public FrameworkElement? Side { get; init; }
    /// <summary>Дясното меню: „Редактирай…“ (null – без).</summary>
    public Action<TaskItem>? Edit { get; init; }
    /// <summary>Може ли да се влачи (свършените в историята – не).</summary>
    public bool Draggable { get; init; } = true;
    /// <summary>Двойно цъкване по картичката (например – отвори подробностите и пиши в името).</summary>
    public Action<TaskItem>? Open { get; init; }
    /// <summary>▶ при посочване: мечо-доро по задачата.</summary>
    public bool PlayButton { get; init; } = true;
}

/// <summary>
/// Картичка на задача и влаченето ѝ. Картичката: цветна черта на етикета, квадратче за
/// отметка, името (задраскано, ако е свършена), описанието, а отдолу – етикет, големина,
/// време и колко пъти е пренасяна; звездичката вдясно горе се цъка (важна). С дясното копче –
/// всичко останало (етикет, време, премести за утре/„Някога“, мечо-доро, изтрий).
/// </summary>
internal static class TaskCards
{
    public const string DragFormat = "MechoTask";

    /// <summary>Колко време може да отнеме една задача (в минути) – копчетата за избор.</summary>
    public static readonly int[] Estimates = { 15, 30, 45, 60, 90, 120, 180 };

    /// <summary>15м, 1ч, 1ч 30м.</summary>
    public static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes}м" : minutes % 60 == 0 ? $"{minutes / 60}ч" : $"{minutes / 60}ч {minutes % 60}м";

    public static readonly string[] SizeNames = { "малка", "средна", "голяма" };

    private static double U => Body / 18;

    /// <summary>Пикселно квадратче за отметка: празно или медено с отметка.</summary>
    public static Border CheckSquare(bool on, Action<bool> toggled, bool enabled = true)
    {
        var box = new Border
        {
            Width = 5 * P,
            Height = 5 * P,
            Background = on ? Honey : Cream,
            BorderBrush = Ink,
            BorderThickness = Px(1),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 7 * U, 0),
            Cursor = enabled ? Cursors.Hand : null,
            Child = on ? Icon("check") : null,
        };
        if (box.Child is FrameworkElement check)
        {
            check.Width = check.Height = 3 * P;
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
        }
        if (!enabled) return box;
        box.MouseEnter += (_, _) => { if (!on) box.Background = Hover; };
        box.MouseLeave += (_, _) => box.Background = on ? Honey : Cream;
        box.MouseLeftButtonDown += (_, e) => e.Handled = true;
        box.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            toggled(!on);
        };
        return box;
    }

    /// <summary>Големината – три пикселни квадратчета (пълните са колкото е голяма).</summary>
    public static FrameworkElement SizeDots(int size)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8 * U, 0) };
        for (int i = 1; i <= 3; i++)
            row.Children.Add(new Border
            {
                Width = 2 * P,
                Height = 2 * P,
                Margin = new Thickness(0, 0, P / 2, 0),
                Background = i <= size ? Ink : Brushes.Transparent,
                BorderBrush = Ink,
                BorderThickness = new Thickness(Math.Max(1, P / 2)),
                Opacity = i <= size ? 0.75 : 0.35,
            });
        row.ToolTip = Tip($"{SizeNames[Math.Clamp(size, 1, 3) - 1]} задача · +{(size switch { 3 => 5, 2 => 3, _ => 1 })} лешника");
        return row;
    }

    /// <summary>Етикет: цветна лентичка с името.</summary>
    public static FrameworkElement TagPill(string key)
    {
        var info = TaskTags.Find(key);
        var pill = new Border
        {
            Background = TaskTags.BrushOf(key, 70),
            BorderBrush = TaskTags.BrushOf(key),
            BorderThickness = new Thickness(Math.Max(1, P / 2)),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(5 * U, 0, 5 * U, 1),
            Margin = new Thickness(0, 0, 8 * U, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = info?.Name ?? key, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis },
        };
        return pill;
    }

    private static FrameworkElement MetaItem(string icon, string text, string tip)
    {
        var item = IconText(icon, text);
        item.Opacity = 0.8;
        item.Margin = new Thickness(0, 0, 8 * U, 2);
        item.ToolTip = Tip(tip);
        return item;
    }

    /// <summary>Редът под името: етикет, големина, стъпки, време (работено/планирано), пренасяна.</summary>
    public static FrameworkElement? Meta(TaskItem t, PetWindow? pet = null)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 3 * U, 0, 0) };
        if (t.Tag.Length > 0)
        {
            var tag = TagPill(t.Tag);
            // Цъкнеш етикета – малък избор точно там (като в Sunsama).
            if (pet != null) Clickable(tag, () => PixelPopup.Show(tag, "Етикет", TagEntries(pet, t)));
            row.Children.Add(tag);
        }
        row.Children.Add(SizeDots(t.Size));
        var steps = t.StepList;
        if (steps.Count > 0)
            row.Children.Add(MetaItem("check", $"{steps.Count(x => x.Done)}/{steps.Count}", "Стъпки: свършени от всички"));
        if (t.Spent > 0)
            row.Children.Add(MetaItem("hourglass", t.Minutes > 0 ? $"{Duration(t.Spent)}/{Duration(t.Minutes)}" : Duration(t.Spent),
                $"Работено {Duration(t.Spent)}" + (t.Minutes > 0 ? $" от планираните {Duration(t.Minutes)}" : "")));
        else if (t.Minutes > 0)
        {
            var time = MetaItem("clock", Duration(t.Minutes), $"Ще отнеме около {Duration(t.Minutes)}");
            if (pet != null) Clickable(time, () => PixelPopup.Show(time, "Колко време?", TimeEntries(pet, t)));
            row.Children.Add(time);
        }
        if (t.Rolled > 0 && !t.Done)
            row.Children.Add(MetaItem("history", t.Rolled.ToString(), t.Rolled == 1
                ? "Пренесена от вчера."
                : $"Пренасяна {t.Rolled} пъти от ден за ден. Може би е голяма – раздели я на стъпки или я сложи за „Някога“."));
        return row;
    }

    /// <summary>Картичката на задачата.</summary>
    public static Border Build(PetWindow pet, TaskItem t, TaskCardOptions? o = null)
    {
        o ??= new TaskCardOptions();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // черта
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // отметка
        grid.ColumnDefinitions.Add(new ColumnDefinition());                             // текст
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // звезда
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // копчета

        // Цветна черта отляво – етикетът.
        var stripe = new Border
        {
            Width = 1.5 * P,
            Margin = new Thickness(-4 * U, -3 * U, 6 * U, -3 * U),
            Background = t.Tag.Length > 0 ? TaskTags.BrushOf(t.Tag) : Brushes.Transparent,
            CornerRadius = new CornerRadius(2),
        };
        grid.Children.Add(stripe);

        bool live = pet.Save.Tasks.Any(x => x.Id == t.Id);   // в историята (минал ден) – само за гледане
        var check = CheckSquare(t.Done, on => pet.SetTaskDone(t, on), enabled: live);
        Grid.SetColumn(check, 1);
        grid.Children.Add(check);

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = t.Title,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
            Foreground = t.Done ? Muted : Ink,
        });
        if (o.Description > 0 && t.Description.Length > 0 && !t.Done)
        {
            text.Children.Add(new TextBlock { Text = Shorten(t.Description, o.Description), Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            if (t.Description.Length > o.Description)
            {
                text.ToolTip = Tip(t.Description);
                ToolTipService.SetInitialShowDelay(text, 250);
                ToolTipService.SetShowDuration(text, 60000);
            }
        }
        if (!t.Done && Meta(t, live ? pet : null) is { } meta) text.Children.Add(meta);
        Grid.SetColumn(text, 2);
        grid.Children.Add(text);

        // Звездичката: пълна – важна; иначе се показва бледа при посочване.
        var star = Icon("star");
        var starBox = new Border
        {
            Child = star,
            Background = Brushes.Transparent,
            Margin = new Thickness(4 * U, 0, 0, 0),
            Cursor = live && !t.Done ? Cursors.Hand : null,
            Opacity = t.Star ? 1 : 0,
            ToolTip = Tip(t.Star ? "Важна (цъкни, за да я махнеш)" : "Направи я важна"),
        };
        if (live && !t.Done)
        {
            starBox.MouseLeftButtonDown += (_, e) => e.Handled = true;
            starBox.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                pet.MarkTask(t, star: !t.Star);
            };
        }
        var corner = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        corner.Children.Add(starBox);
        // Мечо-дорото не се пуска от задачата – от „Фокус“ (там се избира и задачата).
        // „…“ при посочване – всичко за задачата в малко меню (като в Sunsama).
        Border? play = null;
        bool current = false;
        if (live)
        {
            play = new Border
            {
                Child = new TextBlock { Text = "···", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                Background = Brushes.Transparent,
                Margin = new Thickness(4 * U, 4 * U, 0, 0),
                Padding = new Thickness(2, 0, 2, 2),
                Cursor = Cursors.Hand,
                Opacity = 0,
                ToolTip = Tip("Още за задачата"),
            };
            var dots = play;
            Clickable(dots, () => PixelPopup.Show(dots, null, TaskEntries(pet, t, o.Edit)));
            corner.Children.Add(play);
        }
        Grid.SetColumn(corner, 3);
        grid.Children.Add(corner);

        if (o.Side != null)
        {
            Grid.SetColumn(o.Side, 4);
            grid.Children.Add(o.Side);
        }

        var card = MenuWindow.Card(grid, t.Done);
        card.Padding = new Thickness(6 * U, 5 * U, 5 * U, 5 * U);
        card.Margin = new Thickness(0, 0, 0, 5 * U);
        card.Tag = t;
        if (o.Selected)
        {
            card.BorderBrush = Ink;
            card.BorderThickness = Px(1);
            card.Background = Cream;
        }
        else if (!t.Done)
        {
            var normal = card.Background;
            card.MouseEnter += (_, _) => card.Background = Hover;
            card.MouseLeave += (_, _) => card.Background = normal;
        }
        card.MouseEnter += (_, _) =>
        {
            if (!t.Star && live && !t.Done) starBox.Opacity = 0.35;
            if (play != null) play.Opacity = 0.8;
        };
        card.MouseLeave += (_, _) =>
        {
            if (!t.Star) starBox.Opacity = 0;
            if (play != null) play.Opacity = current ? 1 : 0;
        };

        if (o.Open != null)
            card.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount != 2 || e.Handled) return;
                o.Open(t);
                e.Handled = true;
            };
        if (o.Select != null)
        {
            card.MouseLeftButtonUp += (_, e) =>
            {
                if (e.Handled) return;
                o.Select(t);
                e.Handled = true;
            };
        }
        if (live)
        {
            card.MouseRightButtonUp += (_, e) =>
            {
                PixelPopup.Show(card, null, TaskEntries(pet, t, o.Edit), atMouse: true);
                e.Handled = true;
            };
            if (o.Draggable) Draggable(card, t.Id);
        }
        return card;
    }

    // ───────────────────────── Малките менюта (···, етикет, време) ─────────────────────────

    /// <summary>Копче без рамка: цъкане (не стига до картичката).</summary>
    public static void Clickable(FrameworkElement e, Action click)
    {
        e.Cursor = Cursors.Hand;
        if (e is Panel panel && panel.Background == null) panel.Background = Brushes.Transparent;
        e.MouseLeftButtonDown += (_, a) => a.Handled = true;
        e.MouseLeftButtonUp += (_, a) =>
        {
            a.Handled = true;
            click();
        };
    }

    private static TaskItem Fresh(PetWindow pet, TaskItem t) => pet.Save.Tasks.FirstOrDefault(x => x.Id == t.Id) ?? t;

    public static Func<IEnumerable<PixelPopup.Entry>> TagEntries(PetWindow pet, TaskItem t) => () =>
        TaskTags.All.Select(tag => new PixelPopup.Entry(tag.Name, null, () => pet.MarkTask(Fresh(pet, t), tag: Fresh(pet, t).Tag == tag.Key ? "" : tag.Key),
                On: Fresh(pet, t).Tag == tag.Key, Swatch: tag.Key))
            .Append(new PixelPopup.Entry("Без етикет", null, () => pet.MarkTask(Fresh(pet, t), tag: ""), On: Fresh(pet, t).Tag.Length == 0))
            .Append(new PixelPopup.Entry("Нов етикет", "plus", null, Sub: () => new[]
            {
                new PixelPopup.Entry("", null, null, Custom: close => TagEditor.NewTag(pet, key =>
                {
                    close();
                    pet.MarkTask(Fresh(pet, t), tag: key);
                })),
            }))
            .Append(new PixelPopup.Entry("Етикети…", "gear", null, Sub: () => new[]
            {
                new PixelPopup.Entry("", null, null, Custom: _ => TagEditor.Manager(pet)),
            }));

    public static Func<IEnumerable<PixelPopup.Entry>> TimeEntries(PetWindow pet, TaskItem t) => () =>
        Estimates.Select(m => new PixelPopup.Entry(Duration(m), null, () => pet.MarkTask(Fresh(pet, t), minutes: m), On: Fresh(pet, t).Minutes == m))
            .Append(new PixelPopup.Entry("Не знам", null, () => pet.MarkTask(Fresh(pet, t), minutes: 0), On: Fresh(pet, t).Minutes == 0));

    /// <summary>Всичко за задачата: важна, етикет, време, големина, премести, табло, копие, свършена, изтрий.</summary>
    public static Func<IEnumerable<PixelPopup.Entry>> TaskEntries(PetWindow pet, TaskItem t, Action<TaskItem>? edit) => () =>
    {
        var cur = Fresh(pet, t);
        var list = new List<PixelPopup.Entry>();
        if (!cur.Done)
        {
            var today = pet.TaskToday;
            list.Add(new("Премести", "arrow_right", null, Sub: () => new PixelPopup.Entry[]
            {
                new("Днес", "sun", () => pet.MoveTask(Fresh(pet, t), today), On: Fresh(pet, t).Day == today),
                new("Утре", null, () => pet.MoveTask(Fresh(pet, t), today.AddDays(1)), On: Fresh(pet, t).Day == today.AddDays(1)),
                new("Следващата седмица", null, () => pet.MoveTask(Fresh(pet, t), WorkView.WeekOf(today).AddDays(7))),
                new("Някога", null, () => pet.MoveTask(Fresh(pet, t), null), On: Fresh(pet, t).Day == null),
            }));
            list.Add(new(cur.Star ? "Не е важна" : "Важна", "star", () => pet.MarkTask(Fresh(pet, t), star: !Fresh(pet, t).Star), On: cur.Star));
            list.Add(new("Етикет", "pin", null, Sub: TagEntries(pet, t)));
            list.Add(new("Колко време", "clock", null, Sub: TimeEntries(pet, t)));
            list.Add(new("Големина", "nut", null, Sub: () => Enumerable.Range(1, 3).Select(i =>
                new PixelPopup.Entry(SizeNames[i - 1], null, () => pet.MarkTask(Fresh(pet, t), size: i), On: Fresh(pet, t).Size == i))));
            bool onBoard = pet.TaskOnBoard(cur);
            list.Add(new(onBoard ? "Махни от таблото" : "Сложи в таблото", onBoard ? "unpin" : "pin", () => pet.SetTaskOnBoard(Fresh(pet, t), !onBoard)));
            list.Add(new("Направи копие", "copy", () => pet.DuplicateTask(Fresh(pet, t))));
            if (edit != null) list.Add(new("Отвори", "pencil", () => edit(Fresh(pet, t))));
            list.Add(new("Свършена", "check", () => pet.SetTaskDone(Fresh(pet, t), true)));
        }
        else list.Add(new("Не е свършена", "reroll", () => pet.SetTaskDone(Fresh(pet, t), false)));
        list.Add(new("Изтрий", "close", () => pet.DeleteTask(Fresh(pet, t)), Danger: true));
        return list;
    };

    // ───────────────────────── Дясното меню ─────────────────────────

    private static MenuItem Item(string text, Action? click, bool on = false, bool enabled = true)
    {
        var item = new MenuItem
        {
            Header = text,
            IsCheckable = false,
            IsChecked = on,
            IsEnabled = enabled,
            FontFamily = GameFont,
            FontSize = Body,
            Foreground = Ink,
        };
        if (click != null) item.Click += (_, _) => click();
        return item;
    }

    /// <summary>Дясното копче на задача: важна, етикет, време, големина, премести, мечо-доро, изтрий.</summary>
    public static ContextMenu Menu(PetWindow pet, TaskItem t, Action<TaskItem>? edit)
    {
        var menu = new ContextMenu
        {
            FontFamily = GameFont,
            FontSize = Body,
            Foreground = Ink,
            Background = Cream,
            BorderBrush = Ink,
            BorderThickness = Px(1),
        };
        // Задачата може да се е сменила, докато менюто е било затворено – взимаме я наново.
        TaskItem Now() => pet.Save.Tasks.FirstOrDefault(x => x.Id == t.Id) ?? t;
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            var cur = Now();
            if (!cur.Done)
            {
                menu.Items.Add(Item(cur.Star ? "Не е важна" : "Важна", () => pet.MarkTask(Now(), star: !Now().Star), cur.Star));

                var tags = Item("Етикет", null);
                foreach (var tag in TaskTags.All)
                {
                    var key = tag.Key;
                    var ti = Item(tag.Name, () => pet.MarkTask(Now(), tag: Now().Tag == key ? "" : key), cur.Tag == key);
                    ti.Icon = TaskTags.Swatch(key);
                    tags.Items.Add(ti);
                }
                tags.Items.Add(new Separator());
                tags.Items.Add(Item("Без етикет", () => pet.MarkTask(Now(), tag: ""), cur.Tag.Length == 0));
                menu.Items.Add(tags);

                var time = Item("Време", null);
                foreach (int m in Estimates)
                {
                    int minutes = m;
                    time.Items.Add(Item(Duration(m), () => pet.MarkTask(Now(), minutes: minutes), cur.Minutes == m));
                }
                time.Items.Add(new Separator());
                time.Items.Add(Item("Не знам", () => pet.MarkTask(Now(), minutes: 0), cur.Minutes == 0));
                menu.Items.Add(time);

                var size = Item("Големина", null);
                for (int i = 1; i <= 3; i++)
                {
                    int s = i;
                    size.Items.Add(Item(SizeNames[i - 1], () => pet.MarkTask(Now(), size: s), cur.Size == i));
                }
                menu.Items.Add(size);

                var today = pet.TaskToday;
                var move = Item("Премести", null);
                move.Items.Add(Item("Днес", () => pet.MoveTask(Now(), today), cur.Day == today));
                if (cur.Day is { } d0 && d0 >= today)
                    move.Items.Add(Item("Ден напред", () => pet.MoveTask(Now(), (Now().Day ?? today).AddDays(1))));
                move.Items.Add(Item("Утре", () => pet.MoveTask(Now(), today.AddDays(1)), cur.Day == today.AddDays(1)));
                var monday = WorkView.WeekOf(today).AddDays(7);
                move.Items.Add(Item("Следващата седмица", () => pet.MoveTask(Now(), monday), cur.Day == monday));
                move.Items.Add(Item("Някога", () => pet.MoveTask(Now(), null), cur.Day == null));
                menu.Items.Add(move);

                bool onBoard = pet.TaskOnBoard(cur);
                menu.Items.Add(Item(onBoard ? "Махни от таблото" : "Сложи в таблото", () => pet.SetTaskOnBoard(Now(), !onBoard)));
                menu.Items.Add(Item("Направи копие", () => pet.DuplicateTask(Now())));
                menu.Items.Add(new Separator());
                if (edit != null) menu.Items.Add(Item("Редактирай…", () => edit(Now())));
                menu.Items.Add(Item("Свършена", () => pet.SetTaskDone(Now(), true)));
            }
            else
            {
                menu.Items.Add(Item("Не е свършена", () => pet.SetTaskDone(Now(), false)));
            }
            menu.Items.Add(Item("Изтрий", () => pet.DeleteTask(Now())));
        };
        menu.Items.Add(new MenuItem());   // да се отвори (съдържанието се строи при Opened)
        return menu;
    }

    // ───────────────────────── Влачене ─────────────────────────

    /// <summary>Влачи ли се задача точно сега (докато трае – списъците не се строят наново).</summary>
    public static bool Dragging { get; private set; }

    /// <summary>Картичката се влачи с лявото копче (не от отметката, звездата и копчетата).</summary>
    public static void Draggable(FrameworkElement card, Guid id)
    {
        Point? start = null;
        card.PreviewMouseLeftButtonDown += (_, e) => start = OnButton(e.OriginalSource as DependencyObject, card) ? null : e.GetPosition(card);
        card.PreviewMouseLeftButtonUp += (_, _) => start = null;
        card.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } s || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(card);
            if (Math.Abs(p.X - s.X) < SystemParameters.MinimumHorizontalDragDistance * 2
                && Math.Abs(p.Y - s.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;
            start = null;
            Dragging = true;
            var ghost = DragGhost.Start(card, s);
            card.Opacity = 0.35;
            GiveFeedbackEventHandler follow = (_, _) => ghost?.Follow();
            card.GiveFeedback += follow;
            try
            {
                DragDrop.DoDragDrop(card, new DataObject(DragFormat, id.ToString()), DragDropEffects.Move);
            }
            finally
            {
                card.GiveFeedback -= follow;
                ghost?.Close();
                Dragging = false;
                card.Opacity = 1;
                DragEnded?.Invoke();
            }
        };
    }

    /// <summary>Влаченето свърши (пуснато или отказано) – който е чакал, да се опресни.</summary>
    public static event Action? DragEnded;

    /// <summary>Натиснато ли е върху копче (всичко с ръчичка), а не върху самата картичка.</summary>
    private static bool OnButton(DependencyObject? e, FrameworkElement card)
    {
        while (e != null && e != card)
        {
            if (e is FrameworkElement f && f.Cursor == Cursors.Hand) return true;
            if (e is TextBox) return true;
            e = e is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(e) : LogicalTreeHelper.GetParent(e);
        }
        return false;
    }

    public static Guid? DraggedId(DragEventArgs e) =>
        e.Data.GetDataPresent(DragFormat) && Guid.TryParse(e.Data.GetData(DragFormat) as string, out var id) ? id : null;

    /// <summary>
    /// Списък, в който се пускат задачи: area е цялата колона (там се пуска), list – картичките
    /// (по тях се гледа мястото; медена черта показва къде ще падне). drop(задача, преди коя).
    /// </summary>
    public static void DropTarget(FrameworkElement area, Panel list, Action<Guid, Guid?> drop, ScrollViewer? scroll = null)
    {
        area.AllowDrop = true;
        var marker = new Border
        {
            Height = 1.5 * P,
            Background = Honey,
            BorderBrush = Ink,
            BorderThickness = new Thickness(0, Math.Max(1, P / 2), 0, Math.Max(1, P / 2)),
            Margin = new Thickness(0, -1 * U, 0, 4 * U),
            IsHitTestVisible = false,
        };
        void Remove()
        {
            list.Children.Remove(marker);
            area.Tag = null;
        }
        // Мястото: преди първата картичка, чиято среда е под мишката.
        int Place(DragEventArgs e)
        {
            for (int i = 0; i < list.Children.Count; i++)
                if (list.Children[i] is FrameworkElement { Tag: TaskItem } c && c.IsVisible && e.GetPosition(c).Y < c.ActualHeight / 2) return i;
            return list.Children.Count;
        }
        area.DragOver += (_, e) =>
        {
            e.Handled = true;
            if (DraggedId(e) == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            e.Effects = DragDropEffects.Move;
            list.Children.Remove(marker);
            int at = Math.Min(Place(e), list.Children.Count);
            list.Children.Insert(at, marker);
            area.Tag = "drop";
            if (scroll != null) AutoScroll(scroll, e);
        };
        area.DragLeave += (_, e) =>
        {
            // DragLeave идва и при минаване от картичка на картичка – махаме чертата само навън.
            var p = e.GetPosition(area);
            if (p.X < 0 || p.Y < 0 || p.X >= area.ActualWidth || p.Y >= area.ActualHeight) Remove();
        };
        area.Drop += (_, e) =>
        {
            e.Handled = true;
            list.Children.Remove(marker);
            int at = Place(e);
            Remove();
            if (DraggedId(e) is not { } id) return;
            Guid? before = null;
            for (int i = at; i < list.Children.Count; i++)
                if (list.Children[i] is FrameworkElement { Tag: TaskItem t } && t.Id != id)
                {
                    before = t.Id;
                    break;
                }
            drop(id, before);
        };
    }

    /// <summary>Близо до горния/долния ръб при влачене – списъкът сам се превърта.</summary>
    public static void AutoScroll(ScrollViewer scroll, DragEventArgs e)
    {
        var p = e.GetPosition(scroll);
        double edge = 28 * U;
        if (p.Y < edge) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 12 * U);
        else if (p.Y > scroll.ActualHeight - edge) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 12 * U);
    }
}

/// <summary>
/// „Призракът“ при влачене: снимка на картичката, леко наклонена, която следва мишката
/// (прозорец, през който кликовете минават – иначе пускането би паднало върху него).
/// </summary>
internal sealed class DragGhost
{
    private readonly Window _window;
    private readonly Point _grab;
    private readonly double _dpi;

    private DragGhost(Window window, Point grab, double dpi)
    {
        _window = window;
        _grab = grab;
        _dpi = dpi;
    }

    public static DragGhost? Start(FrameworkElement card, Point grab)
    {
        try
        {
            if (card.ActualWidth < 1 || card.ActualHeight < 1) return null;
            var dpi = VisualTreeHelper.GetDpi(card);
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(card.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(card.ActualHeight * dpi.DpiScaleY),
                96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            bmp.Render(card);
            bmp.Freeze();
            var image = new System.Windows.Controls.Image
            {
                Source = bmp,
                Width = card.ActualWidth,
                Height = card.ActualHeight,
                RenderTransform = new RotateTransform(2, card.ActualWidth / 2, card.ActualHeight / 2),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 0, ShadowDepth = 4, Opacity = 0.35, Direction = 300 },
            };
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = new Border { Child = image, Padding = new Thickness(6), Opacity = 0.9 },
                IsHitTestVisible = false,
            };
            w.SourceInitialized += (_, _) => NativeMethods.MakeClickThrough(new System.Windows.Interop.WindowInteropHelper(w).Handle);
            var ghost = new DragGhost(w, grab, dpi.DpiScaleX);
            ghost.Follow();
            w.Show();
            return ghost;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Follow()
    {
        var p = NativeMethods.CursorPosition();
        _window.Left = p.X / _dpi - _grab.X - 6;
        _window.Top = p.Y / _dpi - _grab.Y - 6;
    }

    public void Close()
    {
        try { _window.Close(); } catch (InvalidOperationException) { }
    }
}


/// <summary>
/// Малко меню, което изскача точно при нещото (като в Sunsama): светла хартия с рамка,
/// редове с иконка и име, отметка за избраното, „▸“ за подменю (отваря се на мястото, с „←“
/// назад). Затваря се при избор или с цъкане встрани.
/// </summary>
internal static class PixelPopup
{
    public sealed record Entry(string Text, string? Icon, Action? Click, bool On = false, Func<IEnumerable<Entry>>? Sub = null,
        bool Danger = false, string? Swatch = null, Func<Action, FrameworkElement>? Custom = null);

    /// <summary>Изскачащо меню със свое съдържание (build получава „затвори“).</summary>
    public static void ShowCustom(FrameworkElement anchor, Func<Action, FrameworkElement> build, bool atMouse = false) =>
        Show(anchor, null, () => new[] { new Entry("", null, null, Custom: build) }, atMouse);

    private static double U => Ui.Body / 18;

    public static void Show(FrameworkElement anchor, string? title, Func<IEnumerable<Entry>> entries, bool atMouse = false)
    {
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = anchor,
            Placement = atMouse ? System.Windows.Controls.Primitives.PlacementMode.MousePoint : System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.None,
        };
        var list = new StackPanel { MinWidth = 170 * U };
        var box = new Border
        {
            Background = Ui.Cream,
            BorderBrush = Ui.Ink,
            BorderThickness = Ui.Px(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4 * U),
            Margin = new Thickness(0, 2, 6, 6),
            Child = list,
            Effect = Ui.HardShadow(),
        };
        TextElement.SetFontFamily(box, Ui.GameFont);
        TextElement.SetFontSize(box, Ui.Body);
        TextElement.SetForeground(box, Ui.Ink);
        TextOptions.SetTextRenderingMode(box, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(box, TextFormattingMode.Display);
        popup.Child = box;
        var stack = new Stack<(string? Title, Func<IEnumerable<Entry>> Entries)>();

        void Fill(string? head, Func<IEnumerable<Entry>> make)
        {
            list.Children.Clear();
            if (stack.Count > 0 || head != null)
            {
                var top = new DockPanel { Margin = new Thickness(4 * U, 2 * U, 4 * U, 4 * U) };
                if (stack.Count > 0)
                {
                    var back = new TextBlock { Text = "←", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 8 * U, 0) };
                    TaskCards.Clickable(back, () =>
                    {
                        var (t, e) = stack.Pop();
                        Fill(t, e);
                    });
                    DockPanel.SetDock(back, Dock.Left);
                    top.Children.Add(back);
                }
                top.Children.Add(new TextBlock { Text = head ?? "", Foreground = Ui.Muted, FontWeight = FontWeights.Bold });
                list.Children.Add(top);
            }
            foreach (var entry in make())
            {
                var en = entry;
                if (en.Custom != null)
                {
                    list.Children.Add(en.Custom(() => popup.IsOpen = false));
                    continue;
                }
                var row = new DockPanel();
                if (en.Swatch != null)
                {
                    var sw = TaskTags.Swatch(en.Swatch);
                    DockPanel.SetDock(sw, Dock.Left);
                    row.Children.Add(sw);
                }
                else
                {
                    FrameworkElement icon = en.Icon != null ? Ui.Icon(en.Icon) : new Border { Width = 12 * Ui.Dpi / Ui.Dpi * Ui.ArtScale(2) / Ui.Dpi };
                    icon.Margin = new Thickness(0, 0, 8 * U, 0);
                    icon.VerticalAlignment = VerticalAlignment.Center;
                    DockPanel.SetDock(icon, Dock.Left);
                    row.Children.Add(icon);
                }
                if (en.Sub != null || en.On)
                {
                    var mark = en.Sub != null ? (FrameworkElement)new TextBlock { Text = "▸", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center } : Ui.Icon("check");
                    mark.Margin = new Thickness(10 * U, 0, 0, 0);
                    mark.VerticalAlignment = VerticalAlignment.Center;
                    DockPanel.SetDock(mark, Dock.Right);
                    row.Children.Add(mark);
                }
                row.Children.Add(new TextBlock
                {
                    Text = en.Text,
                    FontWeight = en.On ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = en.Danger ? Ui.Bad : Ui.Ink,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                var item = new Border
                {
                    Child = row,
                    Padding = new Thickness(6 * U, 4 * U, 8 * U, 5 * U),
                    CornerRadius = new CornerRadius(3),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                };
                item.MouseEnter += (_, _) => item.Background = Ui.Hover;
                item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
                item.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    if (en.Sub != null)
                    {
                        stack.Push((head, make));
                        Fill(en.Text, en.Sub);
                        return;
                    }
                    popup.IsOpen = false;
                    en.Click?.Invoke();
                };
                list.Children.Add(item);
            }
        }

        Fill(title, entries);
        popup.IsOpen = true;
    }
}
