using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Mecho.Core;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Нова задача (като в Sunsama): цъкаш „+ Задача“ и изскача карта с всичко наведнъж –
/// име, ден, колко време, етикет и сложност (малък, среден, голям лешник). Enter или
/// „Добави“ я слага най-отгоре в деня; Esc затваря.
/// </summary>
internal static class TaskForm
{
    private static double U => Body / 18;
    private static readonly CultureInfo Bg = new("bg-BG");

    public static void Show(PetWindow pet, FrameworkElement anchor, DateTime? day, string? tag = null, bool star = false) =>
        PixelPopup.ShowCustom(anchor, close => Build(pet, day, tag, star, close));

    private static FrameworkElement Build(PetWindow pet, DateTime? startDay, string? startTag, bool startStar, Action close)
    {
        DateTime today = pet.TaskToday;
        DateTime? day = startDay;
        int minutes = 0, size = 1;
        string tag = startTag ?? "";
        bool star = startStar, newTagOpen = false;

        var root = new StackPanel { Margin = new Thickness(8 * U, 6 * U, 8 * U, 8 * U), Width = 330 * U };
        root.Children.Add(new TextBlock { Text = "Нова задача", FontWeight = FontWeights.Bold, FontSize = Title, Margin = new Thickness(0, 0, 0, 6 * U) });

        var title = MenuWindow.NewTextInput();
        title.MaxLength = TasksModule.MaxTitle;
        title.Padding = new Thickness(8, 6, 8, 6);
        root.Children.Add(MenuWindow.WithHint(title, "Име – какво ще правиш?"));

        // Редовете отдолу се строят наново при всяка промяна (името си остава).
        var rows = new StackPanel();
        root.Children.Add(rows);

        void Add()
        {
            string text = title.Text.Trim();
            if (text.Length == 0)
            {
                title.BorderBrush = Bad;
                title.Focus();
                return;
            }
            var first = pet.TaskState.Open.FirstOrDefault(t => t.Day == day && !t.Done);
            pet.AddTask(text, size, null, day, someday: day == null, tag: tag.Length > 0 ? tag : null, star: star,
                minutes: minutes, quiet: true, before: first?.Id);
            close();
        }

        Border Pick(string text, bool on, Action click, string? tip = null)
        {
            var chip = Chip(text, click);
            chip.Padding = new Thickness(7, 2, 7, 3);
            chip.Margin = new Thickness(0, 0, 4 * U, 4 * U);
            SetOn(chip, on);
            if (tip != null) chip.ToolTip = Tip(tip);
            return chip;
        }

        TextBlock Label(string text) => new()
        {
            Text = text,
            Foreground = Muted,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8 * U, 0, 3 * U),
        };

        void Fill()
        {
            rows.Children.Clear();

            // Ден: Днес, Утре, Някога или който и да е ден (◀ ▶).
            rows.Children.Add(Label("Ден"));
            var dayRow = new WrapPanel();
            dayRow.Children.Add(Pick("Днес", day == today, () => { day = today; Fill(); }));
            dayRow.Children.Add(Pick("Утре", day == today.AddDays(1), () => { day = today.AddDays(1); Fill(); }));
            dayRow.Children.Add(Pick("Някога", day == null, () => { day = null; Fill(); }, "Без ден – когато му дойде времето"));
            var other = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4 * U, 0, 0, 4 * U) };
            var prev = Pick("◀", false, () => { day = (day ?? today).AddDays(-1) < today ? today : (day ?? today).AddDays(-1); Fill(); }, "Ден назад");
            var next = Pick("▶", false, () => { day = (day ?? today).AddDays(1); Fill(); }, "Ден напред");
            prev.Margin = next.Margin = new Thickness(0, 0, 0, 0);
            var when = new TextBlock
            {
                Text = day is { } d ? d.ToString("ddd, d MMM", Bg) : "—",
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 70 * U,
                TextAlignment = TextAlignment.Center,
            };
            other.Children.Add(prev);
            other.Children.Add(when);
            other.Children.Add(next);
            dayRow.Children.Add(other);
            rows.Children.Add(dayRow);

            // Колко време.
            rows.Children.Add(Label("Време"));
            var timeRow = new WrapPanel();
            foreach (var m in TaskCards.Estimates)
            {
                int mm = m;
                timeRow.Children.Add(Pick(TaskCards.Duration(m), minutes == m, () => { minutes = minutes == mm ? 0 : mm; Fill(); }));
            }
            timeRow.Children.Add(Pick("?", minutes == 0, () => { minutes = 0; Fill(); }, "Не знам колко"));
            rows.Children.Add(timeRow);

            // Етикет: своите (цвят + име), без, „+“ – нов (тук, под тях).
            rows.Children.Add(Label("Етикет"));
            var tagRow = new WrapPanel();
            foreach (var info in TaskTags.All)
            {
                string key = info.Key;
                var chip = Pick("", tag == key, () => { tag = tag == key ? "" : key; Fill(); });
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(TaskTags.Swatch(key));
                content.Children.Add(new TextBlock { Text = info.Name, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                chip.Child = content;
                tagRow.Children.Add(chip);
            }
            tagRow.Children.Add(Pick("Без", tag.Length == 0, () => { tag = ""; Fill(); }));
            var plus = Pick("", newTagOpen, () => { newTagOpen = !newTagOpen; Fill(); }, "Нов етикет");
            plus.Child = Icon("plus");
            tagRow.Children.Add(plus);
            rows.Children.Add(tagRow);
            if (newTagOpen)
            {
                var form = TagEditor.NewTag(pet, key =>
                {
                    tag = key;
                    newTagOpen = false;
                    Fill();
                    title.Focus();
                });
                form.Margin = new Thickness(0, 2 * U, 0, 0);
                rows.Children.Add(new Border
                {
                    Background = Empty,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(2 * U),
                    Child = form,
                });
            }

            // Сложност: малък, среден, голям лешник (колкото по-голяма – толкова повече лешници).
            rows.Children.Add(Label("Сложност"));
            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 1; i <= 3; i++)
            {
                int n = i;
                int nuts = n switch { 3 => 5, 2 => 3, _ => 1 };
                var chip = Pick("", size == n, () => { size = n; Fill(); }, $"{Capital(TaskCards.SizeNames[n - 1])} – +{nuts} лешника, когато я свършиш");
                var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var nut = Icon("nut", n);
                nut.HorizontalAlignment = HorizontalAlignment.Center;
                // Иконките стоят на една линия отдолу (по-голямата е по-висока).
                var box = new Grid { Height = 12.0 * (ArtScale(1) + 2) / Dpi + 2, MinWidth = 52 * U };
                nut.VerticalAlignment = VerticalAlignment.Bottom;
                box.Children.Add(nut);
                content.Children.Add(box);
                content.Children.Add(new TextBlock { Text = TaskCards.SizeNames[n - 1], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2 * U, 0, 0) });
                chip.Child = content;
                chip.Margin = new Thickness(0, 0, 6 * U, 0);
                sizeRow.Children.Add(chip);
            }
            var starChip = Pick("", star, () => { star = !star; Fill(); }, "Важна");
            starChip.Child = IconText("star", "Важна", bold: true);
            starChip.VerticalAlignment = VerticalAlignment.Center;
            starChip.Margin = new Thickness(8 * U, 0, 0, 0);
            sizeRow.Children.Add(starChip);
            rows.Children.Add(sizeRow);

            // Добави / Отказ.
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12 * U, 0, 0) };
            var cancel = Ui.Button("Отказ", close);
            var add = Ui.Button("Добави", Add, primary: true, icon: "plus");
            cancel.Margin = new Thickness(0, 0, 6 * U, 0);
            add.Margin = new Thickness(0);
            buttons.Children.Add(cancel);
            buttons.Children.Add(add);
            rows.Children.Add(buttons);
        }
        Fill();

        root.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && title.IsKeyboardFocused)
            {
                Add();
                e.Handled = true;
            }
        };
        root.Loaded += (_, _) => root.Dispatcher.BeginInvoke(() =>
        {
            title.Focus();
            Keyboard.Focus(title);
        }, DispatcherPriority.Input);
        return root;
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
}
