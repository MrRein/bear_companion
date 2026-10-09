using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Панелът, който се отваря с клик върху мечока: как е той, храна, задачи,
/// мед-доро и останалите опции. Затваря се с Esc, с × или с клик другаде.
/// </summary>
public sealed class MenuWindow : Window
{
    private const double BarWidth = 170;

    public const int BearTab = 0, FoodTab = 1, TasksTab = 2, NotesTab = 3, PomodoroTab = 4, MoreTab = 5;

    private readonly PetWindow _pet;
    private readonly DispatcherTimer _refresh;
    private readonly Border[] _tabButtons = new Border[6];
    private readonly UIElement[] _tabs = new UIElement[6];
    private int _tab;
    private readonly StackPanel _noteList = new();
    private TextBox _noteInput = null!;

    // Динамични части
    private readonly TextBlock _nuts = new();
    private Border _fullBar = null!, _energyBar = null!;
    private TextBlock _fullText = null!, _energyText = null!;
    private readonly StackPanel _foods = new();
    private TextBlock _earnInfo = null!;
    private Button _sleepButton = null!;
    private readonly StackPanel _taskList = new();
    private TextBlock _taskSummary = null!;
    private TextBox _taskInput = null!;
    private int _taskSize = 1;
    private readonly Border[] _sizeButtons = new Border[3];
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
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        Foreground = Ink;
        UseLayoutRounding = true;

        _tabs[BearTab] = BuildBearTab();
        _tabs[FoodTab] = BuildFoodTab();
        _tabs[TasksTab] = BuildTasksTab();
        _tabs[NotesTab] = BuildNotesTab();
        _tabs[PomodoroTab] = BuildPomodoroTab();
        _tabs[MoreTab] = BuildMoreTab();

        var content = new Grid { Width = 430 };
        foreach (var t in _tabs) content.Children.Add(t);

        var tabs = new UniformGrid4();
        string[] names = { "🐻 Мечо", "🍳 Храна", "📋 Задачи", "📝 Бележки", "⏰ Време", "⚙️ Още" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            _tabButtons[i] = Chip(names[i], () => SelectTab(index));
            _tabButtons[i].Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0);
            tabs.Add(_tabButtons[i]);
        }

        var panel = new StackPanel { Margin = new Thickness(12, 8, 12, 12) };
        panel.Children.Add(BuildHeader());
        panel.Children.Add(tabs.Panel);
        panel.Children.Add(new Border { Height = 10 });
        panel.Children.Add(content);

        Content = new Border
        {
            Background = Paper,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(6),
            Child = panel,
        };

        _pet.CareChanged += (_, _) => { if (IsVisible) Refresh(rebuildTasks: true); };
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refresh.Tick += (_, _) => Refresh(rebuildTasks: false);

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

        SelectTab(0);
    }

    /// <summary>
    /// Показва панела до мечока (вдясно или вляво, където има място), за да
    /// се вижда балончето над него.
    /// </summary>
    public void ShowNear(Rect bear, Rect area, int tab)
    {
        SelectTab(tab);
        Show();
        UpdateLayout();

        // Ако Тут е преместила панела, отваря се там (стига екранът още да го има).
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
            _tabs[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            _tabButtons[i].Background = i == index ? Honey : PaperDark;
        }
        Refresh(rebuildTasks: true);
        if (index == TasksTab) Dispatcher.BeginInvoke(() => _taskInput.Focus(), DispatcherPriority.Input);
        if (index == NotesTab) Dispatcher.BeginInvoke(() => _noteInput.Focus(), DispatcherPriority.Input);
    }

    // ───────────────────────── Части ─────────────────────────

    private UIElement BuildHeader()
    {
        // Заглавието е дръжка: за него панелът се мести, а мястото се запомня.
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8), Background = Brushes.Transparent, Cursor = Cursors.SizeAll, ToolTip = "Хвани ме оттук, за да ме преместиш" };
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

        grid.Children.Add(new TextBlock { Text = "Мечо", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });

        _nuts.FontSize = 15;
        _nuts.FontWeight = FontWeights.Bold;
        _nuts.VerticalAlignment = VerticalAlignment.Center;
        _nuts.Margin = new Thickness(0, 0, 10, 0);
        _nuts.ToolTip = "Лешници: печелят се със задачи, мед-доро и таймери; харчат се в 🍳 Храна";
        Grid.SetColumn(_nuts, 1);
        grid.Children.Add(_nuts);

        var close = Chip("✕", Hide);
        close.Padding = new Thickness(7, 1, 7, 2);
        Grid.SetColumn(close, 2);
        grid.Children.Add(close);
        return grid;
    }

    private UIElement BuildBearTab()
    {
        var p = new StackPanel();
        p.Children.Add(Bar("🍯 Ситост", out _fullBar, out _fullText));
        p.Children.Add(Bar("💤 Енергия", out _energyBar, out _energyText));

        var feed = Button("🍳 Нахрани го (кухнята)", () => SelectTab(FoodTab));
        feed.HorizontalAlignment = HorizontalAlignment.Left;
        feed.Margin = new Thickness(0, 8, 0, 0);
        p.Children.Add(feed);

        p.Children.Add(Heading("Грижа"));
        var row = new WrapPanel();
        _sleepButton = Button("", () =>
        {
            if (_pet.IsAsleep) _pet.WakeUp();
            else _pet.GoToSleep();
            Refresh(false);
        });
        row.Children.Add(_sleepButton);
        row.Children.Add(Button("🤗 Погали", _pet.Pet));
        row.Children.Add(Button("🎲 Хвърли зар", _pet.RollDice));
        p.Children.Add(row);
        return p;
    }

    private UIElement BuildTasksTab()
    {
        var p = new StackPanel();

        _taskInput = new TextBox
        {
            FontSize = 13,
            Padding = new Thickness(4, 3, 4, 3),
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            Background = Brushes.White,
        };
        _taskInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) AddTask();
        };
        var hint = new TextBlock
        {
            Text = "Нова задача…",
            Foreground = Muted,
            IsHitTestVisible = false,
            Margin = new Thickness(8, 5, 0, 0),
        };
        _taskInput.TextChanged += (_, _) =>
            hint.Visibility = _taskInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var input = new Grid();
        input.Children.Add(_taskInput);
        input.Children.Add(hint);
        p.Children.Add(input);

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
        var add = Button("➕ Добави", AddTask);
        add.Margin = new Thickness(4, 0, 0, 0);
        sizes.Children.Add(add);
        p.Children.Add(sizes);
        UpdateSizeButtons();

        p.Children.Add(new ScrollViewer
        {
            Content = _taskList,
            MaxHeight = 260,
            Margin = new Thickness(0, 8, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        _taskSummary = new TextBlock { Foreground = Muted, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        p.Children.Add(_taskSummary);
        return p;
    }

    private UIElement BuildPomodoroTab()
    {
        var p = new StackPanel();
        _pomTime = new TextBlock { FontSize = 40, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        _pomStatus = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        p.Children.Add(_pomTime);
        p.Children.Add(_pomStatus);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        _pomStart = Button("▶ Започни 25 минути", _pet.StartFocus);
        _pomStop = Button("■ Спри", _pet.StopPomodoro);
        row.Children.Add(_pomStart);
        row.Children.Add(_pomStop);
        p.Children.Add(row);

        _pomToday = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        p.Children.Add(_pomToday);
        p.Children.Add(new TextBlock
        {
            Text = "25 минути работа, 5 почивка. Мечокът работи до теб и мълчи. Всеки мед-доро носи 1 🌰.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        });

        // Обикновен таймер: за чай, пране, кратка задача…
        p.Children.Add(new Border { Height = 2, Background = PaperDark, Margin = new Thickness(0, 12, 0, 0) });
        p.Children.Add(Heading("⏰ Таймер"));
        _timerTime = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        p.Children.Add(_timerTime);

        _timerLabel = new TextBox
        {
            Padding = new Thickness(4, 3, 4, 3),
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            Background = Brushes.White,
            Margin = new Thickness(0, 4, 0, 6),
        };
        var hint = new TextBlock { Text = "За какво е? (чай, пране… по избор)", Foreground = Muted, IsHitTestVisible = false, Margin = new Thickness(8, 9, 0, 0) };
        _timerLabel.TextChanged += (_, _) =>
            hint.Visibility = _timerLabel.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var labelBox = new Grid();
        labelBox.Children.Add(_timerLabel);
        labelBox.Children.Add(hint);
        p.Children.Add(labelBox);

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
        _timerMinutes = new TextBox
        {
            Width = 50,
            Padding = new Thickness(4, 2, 4, 2),
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            Background = Brushes.White,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _timerMinutes.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) StartCustomTimer();
        };
        custom.Children.Add(_timerMinutes);
        custom.Children.Add(new TextBlock { Text = "мин", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) });
        var start = Button("▶ Пусни", StartCustomTimer);
        start.Margin = new Thickness(0);
        custom.Children.Add(start);
        _timerStop = Button("■ Спри таймера", _pet.StopTimer);
        _timerStop.Margin = new Thickness(6, 0, 0, 0);
        custom.Children.Add(_timerStop);
        p.Children.Add(custom);
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

    private UIElement BuildNotesTab()
    {
        var p = new StackPanel();
        _noteInput = new TextBox
        {
            FontSize = 13,
            Padding = new Thickness(4, 3, 4, 3),
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            Background = Brushes.White,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60,
            MaxHeight = 140,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _noteInput.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                AddNote();
                e.Handled = true;
            }
        };
        var hint = new TextBlock
        {
            Text = "Запиши нещо… (Ctrl+Enter)",
            Foreground = Muted,
            IsHitTestVisible = false,
            Margin = new Thickness(8, 5, 0, 0),
        };
        _noteInput.TextChanged += (_, _) =>
            hint.Visibility = _noteInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var input = new Grid();
        input.Children.Add(_noteInput);
        input.Children.Add(hint);
        p.Children.Add(input);

        var save = Button("📝 Запиши", AddNote);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        save.Margin = new Thickness(0, 6, 0, 0);
        p.Children.Add(save);

        p.Children.Add(new ScrollViewer
        {
            Content = _noteList,
            MaxHeight = 300,
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
            _noteList.Children.Add(new TextBlock
            {
                Text = "Тефтерът е празен. Запиши идея, телефон, мисъл, която те разсейва… Мечокът ще я пази.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
            });
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
                Padding = new Thickness(0),
            });
            body.Children.Add(new TextBlock
            {
                Text = n.Created.ToString("d.MM.yyyy, HH:mm"),
                Foreground = Muted,
                FontSize = 11,
            });
            grid.Children.Add(body);

            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.Padding = new Thickness(5, 0, 5, 1);
            del.VerticalAlignment = VerticalAlignment.Top;
            del.ToolTip = "Изтрий";
            Grid.SetColumn(del, 1);
            grid.Children.Add(del);

            _noteList.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = PaperDark,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 4, 4, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Child = grid,
            });
        }
    }

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
        var hide = Button("🙈 Скрий мечока (иконката е до часовника)", () =>
        {
            Hide();
            _pet.Hide();
        });
        var reset = Button("📍 Панелът да се отваря до мечока", () =>
        {
            _pet.Save.MenuLeft = double.NaN;
            _pet.Save.MenuTop = double.NaN;
            _pet.Persist();
            Hide();
            _pet.OpenMenu(MoreTab);
        });
        foreach (var b in new[] { _stayButton, _quietButton, _updateButton, reset, hide })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            p.Children.Add(b);
        }
        return p;
    }

    // ───────────────────────── Обновяване на панела ─────────────────────────

    private void Refresh(bool rebuildTasks)
    {
        var s = _pet.Save;
        _nuts.Text = $"🌰 {s.Hazelnuts}";

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
            >= 20 => "сънлив",
            _ => "много сънлив!",
        });
        _sleepButton.Content = _pet.IsAsleep ? "☀️ Събуди го" : "🌙 Приспи го";
        _sleepButton.IsEnabled = _pet.CanSleep;

        if (rebuildTasks || _foods.Children.Count == 0) BuildFoods();
        _earnInfo.Text = $"🌰 {s.Hazelnuts} лешника. Печелят се със задачи (1/3/5) и с работа: всеки 10 мин мед-доро или таймер = 1 🌰" +
                         (s.FocusMinutesBank >= 1 ? $" (събрани {s.FocusMinutesBank:0} мин към следващия)." : ".");

        if (rebuildTasks)
        {
            BuildTaskList();
            BuildNoteList();
        }

        var left = _pet.PomodoroLeft;
        switch (s.PomodoroPhase)
        {
            case PomodoroPhase.Focus:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                _pomStatus.Text = "🍯 Работим заедно. Мечокът мълчи.";
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
        else _timerTime.Text = "не тече";
        _timerStop.Visibility = _pet.TimerRunning ? Visibility.Visible : Visibility.Collapsed;

        _stayButton.Content = _pet.StaysPut ? "🚶 Стани от дивана" : "🛋️ Стой тук и почети";
        _quietButton.Content = _pet.IsQuiet ? "🔔 Може да говориш" : "🤫 Тихо за 1 час";
        _updateButton.Content = _pet.Updater.IsAvailable
            ? $"⬆️ Обнови до версия {_pet.Updater.LatestVersion}"
            : $"🔄 Провери за обновление (сега: {Updater.CurrentVersion})";
        _updateButton.IsEnabled = !_pet.Updater.IsBusy;
    }

    private UIElement BuildFoodTab()
    {
        var p = new StackPanel();
        _earnInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        p.Children.Add(_earnInfo);
        p.Children.Add(_foods);
        return new ScrollViewer { Content = p, MaxHeight = 470, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Кухнята: какво може да сготви сега и какво може да се купи.</summary>
    private void BuildFoods()
    {
        var s = _pet.Save;
        _foods.Children.Clear();

        _foods.Children.Add(Heading("Готви"));
        foreach (var food in Kitchen.Foods)
        {
            var f = food;
            int cost = Kitchen.CostOf(f, s);
            string effect = string.Join(", ", new[]
            {
                f.Fullness >= 5 ? $"+{Kitchen.FullnessOf(f, s):0} ситост" : null,
                f.Energy > 0 ? $"+{f.Energy:0} енергия" : null,
            }.Where(x => x != null));

            Button action;
            if (!Kitchen.CanCook(f, s))
            {
                var tool = Kitchen.Find(f.Tool!);
                action = Button($"🔒 трябва {tool?.Icon} {tool?.Name}", () => { });
                action.IsEnabled = false;
            }
            else if (cost == 0 && Kitchen.BerriesIn(s) > 0)
            {
                action = Button($"⏳ след {Kitchen.BerriesIn(s)} мин", () => { });
                action.IsEnabled = false;
            }
            else
            {
                action = Button(cost == 0 ? "Набери (безплатно)" : $"Сготви · {cost} 🌰", () =>
                {
                    // Храната се появява из екрана; панелът се скрива, за да я занесеш.
                    _pet.Cook(f);
                    Hide();
                });
                action.IsEnabled = s.Hazelnuts >= cost;
            }
            _foods.Children.Add(Row($"{f.Icon} {f.Name}", effect, action));
        }

        _foods.Children.Add(Heading("Кухня и подобрения"));
        foreach (var upgrade in Kitchen.Upgrades)
        {
            var u = upgrade;
            Button action;
            if (Kitchen.Owns(s, u.Id))
            {
                action = Button("✓ Имаш", () => { });
                action.IsEnabled = false;
            }
            else
            {
                action = Button($"Купи · {u.Price} 🌰", () => _pet.Buy(u));
                action.IsEnabled = s.Hazelnuts >= u.Price;
            }
            _foods.Children.Add(Row($"{u.Icon} {u.Name}", u.Description, action));
        }
    }

    /// <summary>Ред: заглавие и описание отляво, бутон отдясно.</summary>
    private static UIElement Row(string title, string description, Button action)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        if (description.Length > 0)
            text.Children.Add(new TextBlock { Text = description, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        grid.Children.Add(text);
        action.Margin = new Thickness(6, 0, 0, 0);
        action.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(action, 1);
        grid.Children.Add(action);
        return grid;
    }

    private void BuildTaskList()
    {
        _taskList.Children.Clear();
        var tasks = _pet.Save.Tasks.OrderBy(t => t.Done).ToList();
        if (tasks.Count == 0)
        {
            _taskList.Children.Add(new TextBlock
            {
                Text = "Няма задачи. Напиши какво ще правиш днес, а мечокът ще ти пази списъка.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        foreach (var task in tasks)
        {
            var t = task;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
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
                },
            };
            check.Click += (_, _) => _pet.SetTaskDone(t, check.IsChecked == true);
            row.Children.Add(check);

            var reward = new TextBlock
            {
                Text = $"+{t.Reward} 🌰",
                Foreground = Muted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0),
            };
            Grid.SetColumn(reward, 1);
            row.Children.Add(reward);

            var del = Chip("✕", () => _pet.DeleteTask(t));
            del.Padding = new Thickness(5, 0, 5, 1);
            del.ToolTip = "Изтрий";
            Grid.SetColumn(del, 2);
            row.Children.Add(del);

            _taskList.Children.Add(row);
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

    // ───────────────────────── Малки помощници ─────────────────────────

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 10, 0, 4),
    };

    private static UIElement Bar(string label, out Border fill, out TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        grid.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

        fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        var track = new Border
        {
            Width = BarWidth,
            Height = 14,
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            Child = fill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        value = new TextBlock { Foreground = Muted, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    private static void SetBar(Border fill, TextBlock text, double value, string word)
    {
        fill.Width = Math.Clamp(value, 0, 100) / 100 * (BarWidth - 4);
        fill.Background = value >= 50 ? Good : value >= 25 ? Okay : Bad;
        text.Text = word;
        fill.ToolTip = $"{value:0}%";
    }

    /// <summary>Ред от равни по ширина табове.</summary>
    private sealed class UniformGrid4
    {
        public Grid Panel { get; } = new();

        public void Add(UIElement element)
        {
            Panel.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(element, Panel.Children.Count);
            Panel.Children.Add(element);
        }
    }
}
