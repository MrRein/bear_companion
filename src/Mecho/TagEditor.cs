using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Своите етикети: нов етикет (име + цвят от палитрата) и списък за преименуване, смяна на
/// цвета и махане. Показват се в изскачащите менюта (PixelPopup) – от етикета на задача,
/// от „Етикет“ в подробностите и от лентата на „Седмица“.
/// </summary>
internal static class TagEditor
{
    private static double U => Body / 18;

    /// <summary>Палитрата: големи квадратчета само с цвят; избраното е с медена рамка.</summary>
    public static WrapPanel Palette(Func<Color> current, Action<Color> pick)
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 4 * U, 0, 0) };
        void Fill()
        {
            wrap.Children.Clear();
            foreach (var color in TaskTags.Palette)
            {
                var c = color;
                bool on = current() == c;
                var square = TaskTags.ColorSquare(c, 5);
                square.Margin = new Thickness(0);
                var ring = new Border
                {
                    BorderBrush = on ? Honey : Brushes.Transparent,
                    BorderThickness = new Thickness(2 * U + 1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(1),
                    Margin = new Thickness(0, 0, 2 * U, 2 * U),
                    Child = square,
                    ToolTip = Tip("Този цвят"),
                };
                TaskCards.Clickable(ring, () =>
                {
                    pick(c);
                    Fill();
                });
                wrap.Children.Add(ring);
            }
        }
        Fill();
        wrap.MaxWidth = 6 * (5 * P + 4 * U + 4 + 2 * U) + 2;
        return wrap;
    }

    /// <summary>Нов етикет: име, цвят, „Добави“ (и Enter). done получава ключа.</summary>
    public static FrameworkElement NewTag(PetWindow pet, Action<string> done)
    {
        var p = new StackPanel { Margin = new Thickness(6 * U, 2 * U, 6 * U, 6 * U), MinWidth = 200 * U };
        var name = MenuWindow.NewTextInput();
        name.MaxLength = TaskTags.MaxName;
        p.Children.Add(MenuWindow.WithHint(name, "Име на етикета"));
        var color = TaskTags.NextColor();
        p.Children.Add(Palette(() => color, c => color = c));
        var error = new TextBlock { Foreground = Bad, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4 * U, 0, 0) };
        p.Children.Add(error);
        void Add()
        {
            if (name.Text.Trim().Length == 0)
            {
                name.BorderBrush = Bad;
                name.Focus();
                return;
            }
            var key = pet.AddTaskTag(name.Text, color);
            if (key == null)
            {
                error.Text = $"Най-много {TaskTags.MaxTags} етикета – махни някой.";
                error.Visibility = Visibility.Visible;
                return;
            }
            done(key);
        }
        name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Add();
            e.Handled = true;
        };
        var add = Ui.Button("Добави", Add, primary: true, icon: "plus");
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Margin = new Thickness(0, 6 * U, 0, 0);
        p.Children.Add(add);
        p.Loaded += (_, _) => name.Focus();
        return p;
    }

    /// <summary>Всички етикети: цвят (цъкни – палитра), име (пиши – преименува), ✕ (маха).</summary>
    public static FrameworkElement Manager(PetWindow pet)
    {
        var root = new StackPanel { Margin = new Thickness(6 * U, 2 * U, 6 * U, 6 * U), MinWidth = 230 * U };
        string? openColor = null, confirm = null;
        void Fill()
        {
            root.Children.Clear();
            foreach (var tag in TaskTags.All)
            {
                var info = tag;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4 * U) };
                var sw = TaskTags.ColorSquare(info.Color, 5);
                sw.ToolTip = Tip("Смени цвета");
                TaskCards.Clickable(sw, () =>
                {
                    openColor = openColor == info.Key ? null : info.Key;
                    confirm = null;
                    Fill();
                });
                DockPanel.SetDock(sw, Dock.Left);
                row.Children.Add(sw);
                bool asking = confirm == info.Key;
                var del = new TextBlock
                {
                    Text = asking ? "Махни?" : "✕",
                    FontWeight = FontWeights.Bold,
                    Foreground = asking ? Bad : Muted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8 * U, 0, 2 * U, 0),
                    ToolTip = Tip(asking ? "Цъкни пак: махни етикета (задачите с него остават без етикет)" : "Махни етикета"),
                };
                TaskCards.Clickable(del, () =>
                {
                    if (confirm == info.Key)
                    {
                        confirm = null;
                        pet.DeleteTaskTag(info.Key);
                    }
                    else confirm = info.Key;
                    Fill();
                });
                DockPanel.SetDock(del, Dock.Right);
                row.Children.Add(del);
                var name = MenuWindow.NewTextInput();
                name.Text = info.Name;
                name.MaxLength = TaskTags.MaxName;
                void Rename()
                {
                    if (name.Text.Trim().Length > 0 && name.Text.Trim() != info.Name) pet.RenameTaskTag(info.Key, name.Text);
                }
                name.LostKeyboardFocus += (_, _) => Rename();
                name.KeyDown += (_, e) =>
                {
                    if (e.Key != Key.Enter) return;
                    Rename();
                    Keyboard.ClearFocus();
                    e.Handled = true;
                };
                row.Children.Add(name);
                root.Children.Add(row);
                if (openColor == info.Key)
                {
                    var palette = Palette(() => TaskTags.Find(info.Key)?.Color ?? Colors.Transparent, c =>
                    {
                        pet.RecolorTaskTag(info.Key, c);
                        openColor = null;
                        Dispatcher().BeginInvoke(Fill);
                    });
                    palette.Margin = new Thickness(0, 0, 0, 6 * U);
                    root.Children.Add(palette);
                }
            }
            if (TaskTags.All.Length == 0)
                root.Children.Add(new TextBlock { Text = "Няма етикети.", Foreground = Muted, Margin = new Thickness(0, 0, 0, 4 * U) });
            root.Children.Add(new TextBlock { Text = "Нов етикет", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8 * U, 0, 0) });
            var form = NewTag(pet, _ => Fill());
            form.Margin = new Thickness(0, 2 * U, 0, 0);
            root.Children.Add(form);
        }
        Fill();
        return root;
    }

    private static System.Windows.Threading.Dispatcher Dispatcher() => Application.Current.Dispatcher;
}
