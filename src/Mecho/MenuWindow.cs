using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mecho;

/// <summary>
/// Панелът, който се отваря с клик върху мечока: как е той, храна, задачи,
/// мед-доро и останалите опции. Затваря се с Esc, с × или с клик другаде.
/// </summary>
public sealed class MenuWindow : Window
{
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(59, 36, 20));
    private static readonly Brush Paper = new SolidColorBrush(Color.FromRgb(255, 248, 230));
    private static readonly Brush PaperDark = new SolidColorBrush(Color.FromRgb(240, 224, 196));
    private static readonly Brush Hover = new SolidColorBrush(Color.FromRgb(255, 233, 170));
    private static readonly Brush Honey = new SolidColorBrush(Color.FromRgb(255, 214, 102));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(140, 115, 90));
    private static readonly Brush Good = new SolidColorBrush(Color.FromRgb(120, 180, 80));
    private static readonly Brush Okay = new SolidColorBrush(Color.FromRgb(240, 170, 60));
    private static readonly Brush Bad = new SolidColorBrush(Color.FromRgb(220, 90, 80));

    private const double BarWidth = 170;

    private readonly PetWindow _pet;
    private readonly DispatcherTimer _refresh;
    private readonly Border[] _tabButtons = new Border[4];
    private readonly UIElement[] _tabs = new UIElement[4];
    private int _tab;

    // Динамични части
    private readonly TextBlock _nuts = new();
    private Border _fullBar = null!, _energyBar = null!;
    private TextBlock _fullText = null!, _energyText = null!;
    private readonly StackPanel _foods = new();
    private Button _sleepButton = null!;
    private readonly StackPanel _taskList = new();
    private TextBlock _taskSummary = null!;
    private TextBox _taskInput = null!;
    private int _taskSize = 1;
    private readonly Border[] _sizeButtons = new Border[3];
    private TextBlock _pomTime = null!, _pomStatus = null!, _pomToday = null!;
    private Button _pomStart = null!, _pomStop = null!;
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

        _tabs[0] = BuildBearTab();
        _tabs[1] = BuildTasksTab();
        _tabs[2] = BuildPomodoroTab();
        _tabs[3] = BuildMoreTab();

        var content = new Grid { Width = 320 };
        foreach (var t in _tabs) content.Children.Add(t);

        var tabs = new UniformGrid4();
        string[] names = { "🐻 Мечо", "📋 Задачи", "🍯 Мед-доро", "⚙️ Още" };
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
    public void ShowNear(Rect bear)
    {
        Refresh(rebuildTasks: true);
        Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
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
        if (index == 1) Dispatcher.BeginInvoke(() => _taskInput.Focus(), DispatcherPriority.Input);
    }

    // ───────────────────────── Части ─────────────────────────

    private UIElement BuildHeader()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock { Text = "Мечо", FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });

        _nuts.FontSize = 15;
        _nuts.FontWeight = FontWeights.Bold;
        _nuts.VerticalAlignment = VerticalAlignment.Center;
        _nuts.Margin = new Thickness(0, 0, 10, 0);
        _nuts.ToolTip = "Лешници: печелят се със задачи и мед-дора";
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

        p.Children.Add(Heading("Нахрани го"));
        p.Children.Add(_foods);

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
        return p;
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
        foreach (var b in new[] { _stayButton, _quietButton, _updateButton, hide })
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

        if (rebuildTasks) BuildTaskList();

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

        _stayButton.Content = _pet.StaysPut ? "🚶 Стани от дивана" : "🛋️ Стой тук и почети";
        _quietButton.Content = _pet.IsQuiet ? "🔔 Може да говориш" : "🤫 Тихо за 1 час";
        _updateButton.Content = _pet.Updater.IsAvailable
            ? $"⬆️ Обнови до версия {_pet.Updater.LatestVersion}"
            : $"🔄 Провери за обновление (сега: {Updater.CurrentVersion})";
        _updateButton.IsEnabled = !_pet.Updater.IsBusy;
    }

    private void BuildFoods()
    {
        _foods.Children.Clear();
        var wrap = new WrapPanel();
        foreach (var food in Kitchen.Foods)
        {
            bool unlocked = Kitchen.IsUnlocked(food, _pet.Save);
            var f = food;
            var b = Button($"{food.Icon} {food.Name}", () => _pet.Feed(f));
            if (!unlocked)
            {
                int left = food.UnlockTasks - _pet.Save.TasksDoneTotal;
                b.Content = $"🔒 {food.Name}";
                b.ToolTip = $"Трябва {food.Unlock}: още {left} {(left == 1 ? "задача" : "задачи")}";
                b.IsEnabled = false;
                ToolTipService.SetShowOnDisabled(b, true);
            }
            wrap.Children.Add(b);
        }
        _foods.Children.Add(wrap);

        var next = Kitchen.Foods.FirstOrDefault(f => !Kitchen.IsUnlocked(f, _pet.Save));
        if (next != null)
        {
            int left = next.UnlockTasks - _pet.Save.TasksDoneTotal;
            _foods.Children.Add(new TextBlock
            {
                Text = $"Още {left} {(left == 1 ? "свършена задача" : "свършени задачи")} до {next.Unlock} ({next.Icon} {next.Name.ToLowerInvariant()})!",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 2, 0, 0),
            });
        }
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

    private static Button Button(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(8, 3, 8, 4),
            Margin = new Thickness(0, 0, 4, 4),
            Background = PaperDark,
            Foreground = Ink,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Template = FlatTemplate(),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Малък „бутон“ от Border (за табове, размери и ×).</summary>
    private static Border Chip(string text, Action onClick)
    {
        var chip = new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 3),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
        };
        chip.MouseLeftButtonUp += (_, e) =>
        {
            onClick();
            e.Handled = true;
        };
        return chip;
    }

    /// <summary>Плосък бутон с рамка, който светва при посочване.</summary>
    private static ControlTemplate FlatTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding)) { RelativeSource = RelativeSource.TemplatedParent });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(HorizontalAlignmentProperty, new Binding(nameof(Control.HorizontalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Hover, "bd"));
        template.Triggers.Add(hover);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(OpacityProperty, 0.5));
        template.Triggers.Add(disabled);
        return template;
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
