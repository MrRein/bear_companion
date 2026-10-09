using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Панелът „Мечо“ (десен бутон върху мечока), направен като меню на игра:
/// дървена рамка, табове с иконки, плочки с картинки. Мести се за заглавието
/// и помни мястото си. Затваря се с Esc, с ✕ или с клик другаде.
/// </summary>
public sealed class MenuWindow : Window
{
    public const int BearTab = 0, FoodTab = 1, ShopTab = 2, TasksTab = 3, NotesTab = 4, PomodoroTab = 5, MoreTab = 6;

    private const double ContentWidth = 560;
    private const int Segments = 10; // деленца в лентите за ситост и енергия

    private readonly PetWindow _pet;
    private readonly DispatcherTimer _refresh;
    private readonly Border[] _tabButtons = new Border[7];
    private readonly UIElement[] _tabs = new UIElement[7];
    private int _tab;

    // Динамични части
    private readonly TextBlock _nuts = new() { FontWeight = FontWeights.Normal, FontSize = 15 };
    private Image _portrait = null!;
    private Border[] _fullBar = null!, _energyBar = null!;
    private TextBlock _fullText = null!, _energyText = null!, _mood = null!;
    private Button _sleepButton = null!;
    private readonly WrapPanel _foodTiles = new();
    private readonly WrapPanel _shopTiles = new();
    private TextBlock _foodInfo = null!, _shopInfo = null!;
    private readonly StackPanel _taskList = new();
    private TextBlock _taskSummary = null!;
    private TextBox _taskInput = null!;
    private int _taskSize = 1;
    private readonly Border[] _sizeButtons = new Border[3];
    private readonly StackPanel _noteList = new();
    private TextBox _noteInput = null!;
    private TextBlock _pomTime = null!, _pomStatus = null!, _pomToday = null!;
    private Button _pomStart = null!, _pomStop = null!;
    private TextBlock _timerTime = null!;
    private TextBox _timerLabel = null!, _timerMinutes = null!;
    private Button _timerStop = null!;
    private Button _stayButton = null!, _quietButton = null!, _updateButton = null!;

    public MenuWindow(PetWindow pet)
    {
        _pet = pet;
        Title = "Мечо";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = GameFont;
        FontSize = 10; // Pixeled е най-рязък на 10, 15, 20…
        Foreground = Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);

        _tabs[BearTab] = BuildBearTab();
        _tabs[FoodTab] = BuildFoodTab();
        _tabs[ShopTab] = BuildShopTab();
        _tabs[TasksTab] = BuildTasksTab();
        _tabs[NotesTab] = BuildNotesTab();
        _tabs[PomodoroTab] = BuildPomodoroTab();
        _tabs[MoreTab] = BuildMoreTab();

        var content = new Grid { Width = ContentWidth, MinHeight = 300 };
        foreach (var t in _tabs) content.Children.Add(t);

        var root = new DockPanel();
        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        var tabs = BuildTabs();
        DockPanel.SetDock(tabs, Dock.Top);
        root.Children.Add(tabs);
        root.Children.Add(new Border
        {
            Background = Paper,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(0, 0, 5, 5),
            Padding = new Thickness(12, 10, 12, 12),
            Child = content,
        });

        Content = new Border
        {
            Background = Wood,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(6, 4, 6, 6),
            Child = root,
        };

        _pet.CareChanged += (_, _) => { if (IsVisible) Refresh(rebuild: true); };
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refresh.Tick += (_, _) => Refresh(rebuild: false);

        Deactivated += (_, _) => Hide();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _refresh.Start();
            else
            {
                _refresh.Stop();
                _pet.MenuClosed();
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Hide();
        };

        SelectTab(BearTab);
    }

    /// <summary>
    /// Показва панела там, където Тут го е оставила, или до мечока (вдясно или
    /// вляво, където има място), за да се вижда балончето над него.
    /// </summary>
    public void ShowNear(Rect bear, Rect area, int tab)
    {
        SelectTab(tab);
        Show();
        UpdateLayout();

        var s = _pet.Save;
        if (!double.IsNaN(s.MenuLeft) && !double.IsNaN(s.MenuTop))
        {
            var home = _pet.AreaAt(s.MenuLeft + 20, s.MenuTop + 20);
            if (home.Contains(new Point(s.MenuLeft + 20, s.MenuTop + 20)))
            {
                Left = Math.Clamp(s.MenuLeft, home.Left, Math.Max(home.Left, home.Right - ActualWidth));
                Top = Math.Clamp(s.MenuTop, home.Top, Math.Max(home.Top, home.Bottom - ActualHeight));
                Activate();
                return;
            }
        }

        double left = bear.Right + 8;
        if (left + ActualWidth > area.Right - 4) left = bear.Left - ActualWidth - 8;
        Left = Math.Clamp(left, area.Left + 4, Math.Max(area.Left + 4, area.Right - ActualWidth - 4));
        Top = Math.Max(area.Top + 4, area.Bottom - ActualHeight - 4);
        Activate();
    }

    private void SelectTab(int index)
    {
        _tab = index;
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool on = i == index;
            _tabs[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            // Избраният таб е „слят“ с хартията отдолу, другите са по-тъмни.
            _tabButtons[i].Background = on ? Paper : WoodLight;
            _tabButtons[i].BorderThickness = on ? new Thickness(2, 2, 2, 0) : new Thickness(2);
            _tabButtons[i].Margin = new Thickness(i == 0 ? 0 : 3, on ? 0 : 4, 0, on ? -2 : 0);
        }
        Refresh(rebuild: true);
        if (index == TasksTab) Dispatcher.BeginInvoke(() => _taskInput.Focus(), DispatcherPriority.Input);
        if (index == NotesTab) Dispatcher.BeginInvoke(() => _noteInput.Focus(), DispatcherPriority.Input);
    }

    // ───────────────────────── Рамка: заглавие и табове ─────────────────────────

    private UIElement BuildHeader()
    {
        // Заглавието е дръжка: за него панелът се мести, а мястото се запомня.
        var grid = new Grid
        {
            Margin = new Thickness(4, 0, 0, 6),
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            ToolTip = "Хвани ме оттук, за да ме преместиш",
        };
        grid.MouseLeftButtonDown += (_, e) =>
        {
            try { DragMove(); }
            catch (InvalidOperationException) { return; }
            _pet.Save.MenuLeft = Left;
            _pet.Save.MenuTop = Top;
            _pet.Persist();
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = "МЕЧО",
            FontSize = 20,
            FontWeight = FontWeights.Normal,
            Foreground = Cream,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, ShadowDepth = 2, BlurRadius = 0, Opacity = 0.6 },
        });

        var nuts = Pill(_nuts);
        nuts.Margin = new Thickness(0, 0, 8, 0);
        nuts.ToolTip = "Лешници: печелят се със задачи, мед-доро и таймери";
        Grid.SetColumn(nuts, 1);
        grid.Children.Add(nuts);

        var close = Chip("✕", Hide);
        close.Background = Bad;
        close.Padding = new Thickness(8, 1, 8, 2);
        ((TextBlock)close.Child).Foreground = Cream;
        Grid.SetColumn(close, 2);
        grid.Children.Add(close);
        return grid;
    }

    private UIElement BuildTabs()
    {
        var grid = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        (string Icon, string Name)[] tabs =
        {
            ("🐻", "Мечо"), ("🍳", "Храна"), ("🛒", "Магазин"), ("📋", "Задачи"), ("📝", "Бележки"), ("⏰", "Време"), ("⚙️", "Още"),
        };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            label.Children.Add(new TextBlock { Text = tabs[i].Icon, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
            label.Children.Add(new TextBlock { Text = tabs[i].Name, FontSize = 10, FontWeight = FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center });
            var tab = new Border
            {
                BorderBrush = Ink,
                CornerRadius = new CornerRadius(5, 5, 0, 0),
                Padding = new Thickness(2, 3, 2, 3),
                Cursor = Cursors.Hand,
                Child = label,
            };
            tab.MouseLeftButtonDown += (_, e) => e.Handled = true;
            tab.MouseLeftButtonUp += (_, e) =>
            {
                SelectTab(index);
                e.Handled = true;
            };
            Panel.SetZIndex(tab, 1);
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(tab, i);
            grid.Children.Add(tab);
            _tabButtons[i] = tab;
        }
        return grid;
    }

    // ───────────────────────── 🐻 Мечо ─────────────────────────

    private UIElement BuildBearTab()
    {
        var p = new StackPanel();

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition());

        _portrait = new Image { Width = 128, Height = 128, SnapsToDevicePixels = true };
        RenderOptions.SetBitmapScalingMode(_portrait, BitmapScalingMode.NearestNeighbor);
        top.Children.Add(new Border
        {
            Background = TileArt,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 4),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6),
            Child = _portrait,
        });

        var stats = new StackPanel { Margin = new Thickness(14, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _mood = new TextBlock { FontSize = 15, FontWeight = FontWeights.Normal, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        stats.Children.Add(_mood);
        stats.Children.Add(Bar("🍯 Ситост", out _fullBar, out _fullText));
        stats.Children.Add(Bar("⚡ Енергия", out _energyBar, out _energyText));
        Grid.SetColumn(stats, 1);
        top.Children.Add(stats);
        p.Children.Add(top);

        var row = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        row.Children.Add(Button("🍳 Нахрани", () => SelectTab(FoodTab), primary: true));
        _sleepButton = Button("", () =>
        {
            if (_pet.IsAsleep) _pet.WakeUp();
            else _pet.GoToSleep();
            Refresh(false);
        });
        row.Children.Add(_sleepButton);
        row.Children.Add(Button("🤗 Погали", _pet.Pet));
        row.Children.Add(Button("🎲 Зар", _pet.RollDice));
        p.Children.Add(row);
        return p;
    }

    private static UIElement Bar(string label, out Border[] segments, out TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });

        var track = new StackPanel { Orientation = Orientation.Horizontal };
        segments = new Border[Segments];
        for (int i = 0; i < Segments; i++)
        {
            segments[i] = new Border
            {
                Width = 15,
                Height = 16,
                Margin = new Thickness(0, 0, 2, 0),
                BorderBrush = Ink,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(2),
                Background = Empty,
            };
            track.Children.Add(segments[i]);
        }
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        value = new TextBlock { Foreground = Muted, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    private static void SetBar(Border[] segments, TextBlock text, double value, string word)
    {
        int filled = (int)Math.Ceiling(Math.Clamp(value, 0, 100) / 100 * segments.Length - 0.01);
        var colour = value >= 50 ? Good : value >= 25 ? Okay : Bad;
        for (int i = 0; i < segments.Length; i++) segments[i].Background = i < filled ? colour : Empty;
        text.Text = word;
        ((FrameworkElement)segments[0].Parent).ToolTip = $"{value:0}%";
    }

    // ───────────────────────── 🍳 Храна ─────────────────────────

    private UIElement BuildFoodTab()
    {
        var p = new StackPanel();
        _foodInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 8) };
        p.Children.Add(_foodInfo);
        p.Children.Add(_foodTiles);
        return new ScrollViewer { Content = p, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Плочки с храните: картинка, какво дава и бутон „Сготви“.</summary>
    private void BuildFoodTiles()
    {
        var s = _pet.Save;
        _foodTiles.Children.Clear();
        foreach (var food in Kitchen.Foods)
        {
            var f = food;
            int cost = Kitchen.CostOf(f, s);
            string effect = string.Join("  ", new[]
            {
                f.Fullness >= 5 ? $"🍯+{Kitchen.FullnessOf(f, s):0}" : null,
                f.Energy > 0 ? $"⚡+{f.Energy:0}" : null,
            }.Where(x => x != null));

            Button action;
            bool locked = !Kitchen.CanCook(f, s);
            if (locked)
            {
                var tool = Kitchen.Find(f.Tool!);
                action = Button($"🔒 {tool?.Name}", () => SelectTab(ShopTab));
                action.ToolTip = $"Трябва {tool?.Icon} {tool?.Name}. Цъкни, за да отидеш в магазина.";
            }
            else if (cost == 0 && Kitchen.BerriesIn(s) > 0)
            {
                action = Button($"⏳ {Kitchen.BerriesIn(s)} мин", () => { });
                action.IsEnabled = false;
            }
            else
            {
                action = Button(cost == 0 ? "Набери" : $"Сготви · {cost} 🌰", () =>
                {
                    // Храната се появява из екрана; панелът се скрива, за да я занесеш.
                    _pet.Cook(f);
                    Hide();
                }, primary: true);
                action.IsEnabled = s.Hazelnuts >= cost;
            }
            _foodTiles.Children.Add(MakeTile(_pet.PropImage("food_" + f.Id), f.Name, effect, action, locked));
        }
    }

    // ───────────────────────── 🛒 Магазин ─────────────────────────

    private UIElement BuildShopTab()
    {
        var p = new StackPanel();
        _shopInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 8) };
        p.Children.Add(_shopInfo);
        p.Children.Add(_shopTiles);
        return new ScrollViewer { Content = p, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void BuildShopTiles()
    {
        var s = _pet.Save;
        _shopTiles.Children.Clear();
        foreach (var upgrade in Kitchen.Upgrades)
        {
            var u = upgrade;
            Button action;
            bool owned = Kitchen.Owns(s, u.Id);
            if (owned)
            {
                action = Button("✓ Купено", () => { });
                action.IsEnabled = false;
            }
            else
            {
                action = Button($"Купи · {u.Price} 🌰", () => _pet.Buy(u), primary: true);
                action.IsEnabled = s.Hazelnuts >= u.Price;
            }
            _shopTiles.Children.Add(MakeTile(_pet.PropImage("shop_" + u.Id), u.Name, u.Description, action, dim: false, owned: owned));
        }
    }

    /// <summary>Плочка: картинка на тъмен фон, име, описание и бутон.</summary>
    private static Border MakeTile(BitmapSource art, string name, string description, Button action, bool dim, bool owned = false)
    {
        const double width = 176;
        var image = Pixel(art, 4);
        if (dim) image.Opacity = 0.35;
        var artBox = new Border
        {
            Background = TileArt,
            BorderBrush = WoodDark,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Height = 78,
            Child = image,
        };
        if (owned)
        {
            var g = new Grid();
            g.Children.Add(image);
            g.Children.Add(new TextBlock
            {
                Text = "✓",
                FontSize = 20,
                FontWeight = FontWeights.Normal,
                Foreground = Good,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 4, 0),
            });
            artBox.Child = g;
        }

        var stack = new StackPanel();
        stack.Children.Add(artBox);
        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.Normal,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 1),
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = Muted,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Height = 42,
        });
        action.Margin = new Thickness(0, 4, 0, 0);
        action.HorizontalAlignment = HorizontalAlignment.Stretch;
        action.HorizontalContentAlignment = HorizontalAlignment.Center;
        stack.Children.Add(action);

        return new Border
        {
            Width = width,
            Background = Tile,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 4),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 0, 6, 6),
            Child = stack,
        };
    }

    // ───────────────────────── 📋 Задачи ─────────────────────────

    private UIElement BuildTasksTab()
    {
        var p = new StackPanel();
        _taskInput = NewTextInput();
        _taskInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) AddTask();
        };
        p.Children.Add(WithHint(_taskInput, "Нова задача…"));

        var sizes = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        string[] labels = { "малка +1 🌰", "средна +3 🌰", "голяма +5 🌰" };
        for (int i = 0; i < 3; i++)
        {
            int size = i + 1;
            _sizeButtons[i] = Chip(labels[i], () =>
            {
                _taskSize = size;
                UpdateSizeButtons();
            });
            _sizeButtons[i].Margin = new Thickness(0, 0, 4, 0);
            sizes.Children.Add(_sizeButtons[i]);
        }
        var add = Button("➕ Добави", AddTask, primary: true);
        add.Margin = new Thickness(4, 0, 0, 0);
        sizes.Children.Add(add);
        p.Children.Add(sizes);
        UpdateSizeButtons();

        p.Children.Add(new ScrollViewer
        {
            Content = _taskList,
            MaxHeight = 300,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        _taskSummary = new TextBlock { Foreground = Muted, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        p.Children.Add(_taskSummary);
        return p;
    }

    private void BuildTaskList()
    {
        _taskList.Children.Clear();
        var tasks = _pet.Save.Tasks.OrderBy(t => t.Done).ToList();
        if (tasks.Count == 0)
            _taskList.Children.Add(MutedText("Няма задачи. Напиши какво ще правиш днес, а мечокът ще ти пази списъка."));

        foreach (var task in tasks)
        {
            var t = task;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox
            {
                IsChecked = t.Done,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = t.Title,
                    TextWrapping = TextWrapping.Wrap,
                    TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
                    Foreground = t.Done ? Muted : Ink,
                    Margin = new Thickness(2, 0, 0, 0),
                },
            };
            check.Click += (_, _) => _pet.SetTaskDone(t, check.IsChecked == true);
            row.Children.Add(check);

            var reward = new TextBlock
            {
                Text = $"+{t.Reward} 🌰",
                FontWeight = FontWeights.Normal,
                Foreground = t.Done ? Muted : Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
            };
            Grid.SetColumn(reward, 1);
            row.Children.Add(reward);

            var del = Chip("✕", () => _pet.DeleteTask(t));
            del.Padding = new Thickness(5, 0, 5, 1);
            del.ToolTip = "Изтрий";
            Grid.SetColumn(del, 2);
            row.Children.Add(del);

            _taskList.Children.Add(Card(row, t.Done));
        }

        int today = _pet.Save.Tasks.Count(t => t.Done);
        _taskSummary.Text = $"Свършени днес: {today}   ·   Общо: {_pet.Save.TasksDoneTotal}";
    }

    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(_taskInput.Text)) return;
        _pet.AddTask(_taskInput.Text, _taskSize);
        _taskInput.Clear();
        _taskInput.Focus();
    }

    private void UpdateSizeButtons()
    {
        for (int i = 0; i < 3; i++)
            _sizeButtons[i].Background = i + 1 == _taskSize ? Honey : PaperDark;
    }

    // ───────────────────────── 📝 Бележки ─────────────────────────

    private UIElement BuildNotesTab()
    {
        var p = new StackPanel();
        _noteInput = NewTextInput();
        _noteInput.AcceptsReturn = true;
        _noteInput.TextWrapping = TextWrapping.Wrap;
        _noteInput.MinHeight = 60;
        _noteInput.MaxHeight = 140;
        _noteInput.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _noteInput.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                AddNote();
                e.Handled = true;
            }
        };
        p.Children.Add(WithHint(_noteInput, "Запиши нещо… (Ctrl+Enter)"));

        var save = Button("📝 Запиши", AddNote, primary: true);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        save.Margin = new Thickness(0, 6, 0, 0);
        p.Children.Add(save);

        p.Children.Add(new ScrollViewer
        {
            Content = _noteList,
            MaxHeight = 320,
            Margin = new Thickness(0, 8, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        return p;
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(_noteInput.Text)) return;
        _pet.AddNote(_noteInput.Text);
        _noteInput.Clear();
        _noteInput.Focus();
    }

    private void BuildNoteList()
    {
        _noteList.Children.Clear();
        if (_pet.Save.Notes.Count == 0)
        {
            _noteList.Children.Add(MutedText("Тефтерът е празен. Запиши идея, телефон, мисъл, която те разсейва… Мечокът ще я пази."));
            return;
        }
        foreach (var note in _pet.Save.Notes.ToList())
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var body = new StackPanel();
            // TextBox само за четене, за да може текстът да се маркира и копира.
            body.Children.Add(new TextBox
            {
                Text = n.Text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Ink,
                FontFamily = GameFont,
                Padding = new Thickness(0),
            });
            body.Children.Add(new TextBlock { Text = n.Created.ToString("d.MM.yyyy, HH:mm"), Foreground = Muted, FontSize = 10 });
            grid.Children.Add(body);

            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.Padding = new Thickness(5, 0, 5, 1);
            del.VerticalAlignment = VerticalAlignment.Top;
            del.ToolTip = "Изтрий";
            Grid.SetColumn(del, 1);
            grid.Children.Add(del);

            _noteList.Children.Add(Card(grid, false));
        }
    }

    // ───────────────────────── ⏰ Време ─────────────────────────

    private UIElement BuildPomodoroTab()
    {
        var p = new StackPanel();
        p.Children.Add(Heading("🍯 Мед-доро"));
        _pomTime = new TextBlock { FontSize = 40, FontWeight = FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center };
        _pomStatus = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        p.Children.Add(new Border
        {
            Background = TileArt,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 4),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2, 8, 8),
            Child = new StackPanel { Children = { _pomTime, _pomStatus } },
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        _pomStart = Button("▶ Започни 25 минути", _pet.StartFocus, primary: true);
        _pomStop = Button("■ Спри", _pet.StopPomodoro);
        row.Children.Add(_pomStart);
        row.Children.Add(_pomStop);
        p.Children.Add(row);

        _pomToday = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        p.Children.Add(_pomToday);

        p.Children.Add(Heading("⏰ Таймер"));
        _timerTime = new TextBlock { FontSize = 20, FontWeight = FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center };
        p.Children.Add(_timerTime);

        _timerLabel = NewTextInput();
        var label = WithHint(_timerLabel, "За какво е? (чай, пране… по избор)");
        ((FrameworkElement)label).Margin = new Thickness(0, 4, 0, 6);
        p.Children.Add(label);

        var presets = new WrapPanel();
        foreach (int m in new[] { 5, 10, 15, 30, 45, 60 })
        {
            int minutes = m;
            var chip = Chip($"{m} мин", () => _pet.StartTimer(minutes, _timerLabel.Text));
            chip.Margin = new Thickness(0, 0, 4, 4);
            presets.Children.Add(chip);
        }
        p.Children.Add(presets);

        var custom = new StackPanel { Orientation = Orientation.Horizontal };
        _timerMinutes = NewTextInput();
        _timerMinutes.Width = 56;
        _timerMinutes.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) StartCustomTimer();
        };
        custom.Children.Add(_timerMinutes);
        custom.Children.Add(new TextBlock { Text = "мин", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) });
        var start = Button("▶ Пусни", StartCustomTimer, primary: true);
        start.Margin = new Thickness(0);
        custom.Children.Add(start);
        _timerStop = Button("■ Спри таймера", _pet.StopTimer);
        _timerStop.Margin = new Thickness(6, 0, 0, 0);
        custom.Children.Add(_timerStop);
        p.Children.Add(custom);

        p.Children.Add(new TextBlock
        {
            Text = "Всеки 10 минути мед-доро или таймер носят 1 🌰.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        });
        return p;
    }

    private void StartCustomTimer()
    {
        string text = _timerMinutes.Text.Trim().Replace(',', '.');
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m) && m > 0 && m <= 24 * 60)
        {
            _pet.StartTimer(m, _timerLabel.Text);
            _timerMinutes.Clear();
        }
    }

    // ───────────────────────── ⚙️ Още ─────────────────────────

    private UIElement BuildMoreTab()
    {
        var p = new StackPanel();
        _stayButton = Button("", () =>
        {
            if (_pet.StaysPut) _pet.GetUp();
            else _pet.StayHere();
            Refresh(false);
        });
        _quietButton = Button("", () =>
        {
            _pet.SetQuiet(!_pet.IsQuiet);
            Refresh(false);
        });
        _updateButton = Button("", () =>
        {
            if (_pet.Updater.IsAvailable) _ = _pet.InstallUpdate();
            else _ = _pet.CheckForUpdates(manual: true);
        });
        var reset = Button("📍 Панелът да се отваря до мечока", () =>
        {
            _pet.Save.MenuLeft = double.NaN;
            _pet.Save.MenuTop = double.NaN;
            _pet.Persist();
            Hide();
            _pet.OpenMenu(MoreTab);
        });
        var hide = Button("🙈 Скрий мечока (иконката е до часовника)", () =>
        {
            Hide();
            _pet.Hide();
        });
        foreach (var b in new[] { _stayButton, _quietButton, _updateButton, reset, hide })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 0, 0, 6);
            p.Children.Add(b);
        }
        return p;
    }

    // ───────────────────────── Обновяване ─────────────────────────

    private void Refresh(bool rebuild)
    {
        var s = _pet.Save;
        _nuts.Text = $"🌰 {s.Hazelnuts}";

        // Мечо
        _portrait.Source = _pet.Portrait;
        _mood.Text = _pet.IsAsleep ? "Ззз… спи сладко" :
            s.Fullness < 25 ? "Гладен е!" :
            s.Energy < 25 ? "Много е уморен" :
            s.Fullness >= 70 && s.Energy >= 60 ? "Щастлив е!" : "Добре е";
        SetBar(_fullBar, _fullText, s.Fullness, s.Fullness switch
        {
            >= 80 => "сит",
            >= 50 => "добре",
            >= 25 => "огладнял",
            _ => "гладен!",
        });
        SetBar(_energyBar, _energyText, s.Energy, _pet.IsAsleep ? "спи…" : s.Energy switch
        {
            >= 70 => "бодър",
            >= 40 => "добре",
            >= 25 => "уморен",
            _ => "капнал!",
        });
        _sleepButton.Content = _pet.IsAsleep ? "☀️ Събуди" : "🌙 Приспи";
        _sleepButton.IsEnabled = _pet.CanSleep;

        // Храна и магазин
        _foodInfo.Text = $"Имаш {s.Hazelnuts} 🌰. Сготвеното се появява из екрана: занеси го при мечока.";
        _shopInfo.Text = "Уредите отключват нови ястия, а подобренията помагат на мечока. Лешниците се печелят " +
                         "със задачи (1/3/5) и с работа: всеки 10 мин мед-доро или таймер = 1 🌰" +
                         (s.FocusMinutesBank >= 1 ? $" (събрани {s.FocusMinutesBank:0} мин към следващия)." : ".");
        if (rebuild)
        {
            BuildFoodTiles();
            BuildShopTiles();
            BuildTaskList();
            BuildNoteList();
        }

        // Време
        var left = _pet.PomodoroLeft;
        switch (s.PomodoroPhase)
        {
            case PomodoroPhase.Focus:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                _pomStatus.Text = "Работим заедно. Мечокът мълчи.";
                break;
            case PomodoroPhase.Break:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                _pomStatus.Text = "☕ Почивка: стани, раздвижи се, пийни вода.";
                break;
            default:
                _pomTime.Text = $"{(int)PetWindow.FocusLength.TotalMinutes:00}:00";
                _pomStatus.Text = "Готова ли си?";
                break;
        }
        _pomStart.Visibility = s.PomodoroPhase == PomodoroPhase.Focus ? Visibility.Collapsed : Visibility.Visible;
        _pomStart.Content = s.PomodoroPhase == PomodoroPhase.Break ? "▶ Нов мед-доро" : "▶ Започни 25 минути";
        _pomStop.Visibility = s.PomodoroPhase == PomodoroPhase.Off ? Visibility.Collapsed : Visibility.Visible;
        _pomToday.Text = $"Днес: {s.PomodorosToday} 🍯   ·   Общо: {s.PomodorosTotal}";

        if (_pet.TimerRunning)
        {
            var t = _pet.TimerLeft;
            string clock = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
            _timerTime.Text = s.TimerLabel.Length > 0 ? $"{clock} · {s.TimerLabel}" : clock;
        }
        else _timerTime.Text = "--:--";
        _timerStop.Visibility = _pet.TimerRunning ? Visibility.Visible : Visibility.Collapsed;

        // Още
        _stayButton.Content = _pet.StaysPut ? "🚶 Стани от дивана" : "🛋️ Стой тук и почети";
        _quietButton.Content = _pet.IsQuiet ? "🔔 Може да говориш" : "🤫 Тихо за 1 час";
        _updateButton.Content = _pet.Updater.IsAvailable
            ? $"⬆️ Обнови до версия {_pet.Updater.LatestVersion}"
            : $"🔄 Провери за обновление (сега: {Updater.CurrentVersion})";
        _updateButton.IsEnabled = !_pet.Updater.IsBusy;
    }

    // ───────────────────────── Малки помощници ─────────────────────────

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 15,
        FontWeight = FontWeights.Normal,
        Margin = new Thickness(0, 10, 0, 6),
    };

    private static TextBlock MutedText(string text) => new()
    {
        Text = text,
        Foreground = Ui.Muted,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBox NewTextInput() => new()
    {
        FontFamily = GameFont,
        FontSize = 10,
        Padding = new Thickness(6, 4, 6, 4),
        BorderBrush = Ink,
        BorderThickness = new Thickness(2, 2, 2, 3),
        Background = Cream,
        Foreground = Ink,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>Поле с бледа подсказка, докато е празно.</summary>
    private static UIElement WithHint(TextBox box, string hint)
    {
        var text = new TextBlock { Text = hint, Foreground = Ui.Muted, IsHitTestVisible = false, Margin = new Thickness(9, 6, 0, 0) };
        box.TextChanged += (_, _) => text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var grid = new Grid();
        grid.Children.Add(box);
        grid.Children.Add(text);
        return grid;
    }

    /// <summary>Картичка за задача или бележка.</summary>
    private static Border Card(UIElement child, bool faded) => new()
    {
        Background = faded ? PaperDark : Tile,
        BorderBrush = WoodDark,
        BorderThickness = new Thickness(2, 2, 2, 3),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 5, 5, 5),
        Margin = new Thickness(0, 0, 0, 5),
        Child = child,
    };
}
