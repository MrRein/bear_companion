using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>Къде стои закаченото прозорче и как е (пази се в save.json).</summary>
public sealed class PinState
{
    public bool Open { get; set; }
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double W { get; set; }
    public double H { get; set; }
    public bool Collapsed { get; set; }    // свито до заглавието
    public bool Faded { get; set; }        // полупрозрачно, докато мишката не е отгоре
}

/// <summary>
/// Закачено прозорче със задачите или бележките: стои отгоре, където го сложиш.
/// Удобства: отметка на задача направо оттук, ▶ пуска мечо-доро по задачата,
/// поле за нова задача/бележка, свиване до заглавието, полупрозрачно, размер
/// с дръжката долу вдясно; мястото и размерът се помнят.
/// </summary>
public sealed class PinWindow : Window
{
    public const string Tasks = "tasks", Notes = "notes";

    private readonly PetWindow _pet;
    private readonly string _kind;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly Border _body;
    private readonly TextBox _input;
    private readonly TextBlock _count = new() { Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Border _collapse, _fade;
    private string _shown = "";

    private static double U => Body / 18;

    private PinState State => _kind == Tasks ? _pet.Save.PinTasks : _pet.Save.PinNotes;

    public PinWindow(PetWindow pet, string kind)
    {
        _pet = pet;
        _kind = kind;
        Title = kind == Tasks ? "Задачи" : "Бележки";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        Topmost = true;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        MinWidth = 220 * U;
        MinHeight = 90 * U;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources.Add(typeof(ScrollBar), ScrollBarStyle());
        WindowStartupLocation = WindowStartupLocation.Manual;

        var s = State;
        Width = s.W > 0 ? s.W : 300 * U;
        Height = s.H > 0 ? s.H : 320 * U;
        var area = SystemParameters.WorkArea;
        Left = double.IsNaN(s.X) ? area.Right - Width - 20 : Math.Clamp(s.X, area.Left - Width + 60, area.Right - 60);
        Top = double.IsNaN(s.Y) ? area.Top + 80 : Math.Clamp(s.Y, area.Top, area.Bottom - 40);

        // Заглавие: иконка, име, брой; копчета: свий, прозрачно, затвори. Влачи се оттук.
        var header = new DockPanel { Margin = new Thickness(2, 0, 2, 5), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        var close = Chip("✕", () => _pet.SetPinned(_kind, false));
        close.ToolTip = Tip("Откачи");
        _fade = Chip("👁", () => { State.Faded = !State.Faded; ApplyFade(); _pet.SavePins(); });
        _fade.ToolTip = Tip("Полупрозрачно, когато мишката не е отгоре");
        _collapse = Chip("➖", ToggleCollapse);
        _collapse.ToolTip = Tip("Свий / разгъни");
        var menu = Chip("🐻", () => _pet.OpenMenu(_kind == Tasks ? MenuWindow.TasksTab : MenuWindow.NotesTab));
        menu.ToolTip = Tip("Отвори в менюто");
        foreach (var b in new[] { close, _fade, _collapse, menu })
        {
            b.Margin = new Thickness(4, 0, 0, 0);
            b.Padding = new Thickness(6, 1, 6, 2);
            DockPanel.SetDock(b, Dock.Right);
            header.Children.Add(b);
        }
        var title = IconText(kind == Tasks ? "tasks" : "notes", Title, bold: true);
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        header.Children.Add(_count);
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                ToggleCollapse();
                return;
            }
            try { DragMove(); } catch (InvalidOperationException) { }
            Remember();
        };

        // Поле за ново: Enter добавя (при бележките – Ctrl+Enter, Enter е нов ред).
        _input = MenuWindow.NewTextInput();
        _input.Padding = new Thickness(6, 4, 6, 4);
        if (kind == Notes)
        {
            _input.AcceptsReturn = true;
            _input.TextWrapping = TextWrapping.Wrap;
            _input.MaxHeight = 90 * U;
        }
        _input.PreviewKeyDown += (_, e) =>
        {
            bool add = kind == Tasks ? e.Key == Key.Enter : e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (add)
            {
                AddFromInput();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _input.Clear();
                _sizeRow.Visibility = Visibility.Collapsed;
            }

        };
        // Задачи: след „+“ (или Enter) – колко е трудна: малка / средна / голяма.
        _sizeRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        _sizeRow.Children.Add(new TextBlock { Text = "Колко е трудна?", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) });
        foreach (var (label, size) in new[] { ("малка", 1), ("средна", 2), ("голяма", 3) })
        {
            int sz = size;
            var chip = Chip(label, () => AddTasks(sz));
            chip.Margin = new Thickness(0, 0, 4, 4);
            chip.Padding = new Thickness(6, 1, 6, 2);
            _sizeRow.Children.Add(chip);
        }
        var cancel = Chip("✕", () => _sizeRow.Visibility = Visibility.Collapsed);
        cancel.Margin = new Thickness(0, 0, 0, 4);
        cancel.Padding = new Thickness(6, 1, 6, 2);
        _sizeRow.Children.Add(cancel);
        var inputRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var plus = Chip("+", AddFromInput);
        plus.Margin = new Thickness(4, 0, 0, 0);
        plus.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(plus, Dock.Right);
        inputRow.Children.Add(plus);
        inputRow.Children.Add(MenuWindow.WithHint(_input, kind == Tasks ? "Нова задача…" : "Бележка…"));

        _scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var bodyPanel = new DockPanel();
        DockPanel.SetDock(_sizeRow, Dock.Bottom);
        bodyPanel.Children.Add(_sizeRow);
        DockPanel.SetDock(inputRow, Dock.Bottom);
        bodyPanel.Children.Add(inputRow);
        bodyPanel.Children.Add(_scroll);
        _body = new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8 * U),
            Child = bodyPanel,
        };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_body);
        Content = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 4, 6, 6),
            Child = root,
        };

        MouseEnter += (_, _) => Opacity = 1;
        MouseLeave += (_, _) => ApplyFade();
        LocationChanged += (_, _) => Remember();
        SizeChanged += (_, _) => { if (!State.Collapsed) Remember(); };
        Closed += (_, _) => Remember();
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        ApplyCollapse();
        ApplyFade();
        Refresh();
    }

    private void Remember()
    {
        var s = State;
        s.X = Left;
        s.Y = Top;
        if (!s.Collapsed && !_applying && ActualWidth > 0)
        {
            s.W = ActualWidth;
            s.H = ActualHeight;
        }
    }

    private void ApplyFade()
    {
        SetOn(_fade, State.Faded);
        Opacity = State.Faded && !IsMouseOver ? 0.55 : 1;
    }

    /// <summary>Свива / разгъва (като ➖).</summary>
    public void ToggleCollapse()
    {
        State.Collapsed = !State.Collapsed;
        ApplyCollapse();
        _pet.SavePins();
    }

    private void ApplyCollapse()
    {
        // Докато се свива/разгъва, размерът се мени за миг (ResizeMode, SizeToContent):
        // той не бива да се запомня, иначе разгънатото излиза мъничко.
        double height = State.H;
        _applying = true;
        SetOn(_collapse, State.Collapsed);
        _body.Visibility = State.Collapsed ? Visibility.Collapsed : Visibility.Visible;
        if (State.Collapsed)
        {
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            MinHeight = 0;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            MinHeight = 90 * U;
            State.H = height;
            Height = height > 0 ? height : 320 * U;
        }
        _applying = false;
    }

    private bool _applying;

    private void AddFromInput()
    {
        string text = _input.Text.Trim();
        if (text.Length == 0) return;
        if (_kind == Tasks)
        {
            // Първо пита колко е трудна (копчетата отдолу), после я добавя.
            _sizeRow.Visibility = Visibility.Visible;
            return;
        }
        _pet.AddNote(text);
        _input.Clear();
        _input.Focus();
    }

    private readonly WrapPanel _sizeRow;

    /// <summary>Добавя написаното като задача (или няколко – по ред) с избраната трудност.</summary>
    private void AddTasks(int size)
    {
        string text = _input.Text.Trim();
        _sizeRow.Visibility = Visibility.Collapsed;
        if (text.Length == 0) return;
        foreach (var line in text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)) _pet.AddTask(line, size);
        _input.Clear();
        _input.Focus();
    }

    /// <summary>Строи списъка наново, само ако нещо се е сменило.</summary>
    public void Refresh()
    {
        var s = _pet.Save;
        string sig = _kind == Tasks
            ? string.Join("|", s.Tasks.Where(t => !t.Done).Select(t => t.Id + t.Title + t.Description + t.Size)) + "#" + s.FocusTask + _pet.InFocus
            : string.Join("|", s.Notes.Select(n => n.Id + n.Text));
        if (sig == _shown) return;
        _shown = sig;
        _list.Children.Clear();
        if (_kind == Tasks) BuildTasks();
        else BuildNotes();
    }

    private void BuildTasks()
    {
        var s = _pet.Save;
        var tasks = s.Tasks.Where(t => !t.Done).ToList();
        _count.Text = tasks.Count > 0 ? $"· {tasks.Count}" : "";
        if (tasks.Count == 0) _list.Children.Add(MenuWindow.MutedText("Няма задачи. Напиши една отдолу!"));
        foreach (var task in tasks)
        {
            var t = task;
            bool current = s.FocusTask == t.Id;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var check = new CheckBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = t.Title,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = current ? FontWeights.Bold : FontWeights.Normal,
                    Margin = new Thickness(4, 0, 0, 0),
                },
            };
            check.Click += (_, _) => _pet.SetTaskDone(t, check.IsChecked == true);
            // Описанието (ако има) излиза при посочване; малкото „…“ подсказва, че има още.
            if (t.Description.Length > 0)
            {
                ((TextBlock)check.Content).Text = t.Title + " …";
                check.ToolTip = Tip(t.Description);
                ToolTipService.SetInitialShowDelay(check, 250);
                ToolTipService.SetShowDuration(check, 60000);
            }
            row.Children.Add(check);
            var reward = RewardText(t.Reward);
            reward.Opacity = 0.7;
            reward.Margin = new Thickness(6, 0, 4, 0);
            Grid.SetColumn(reward, 1);
            row.Children.Add(reward);
            // ▶ – мечо-доро по тази задача (или „по нея работим“, ако вече тече).
            var focus = Chip(current && _pet.InFocus ? "★" : "▶", () =>
            {
                if (_pet.InFocus) _pet.SetFocusTask(t);
                else _pet.StartFocusOn(t);
            });
            focus.ToolTip = Tip(_pet.InFocus ? "Сега работим по нея" : "Мечо-доро по тази задача");
            focus.Padding = new Thickness(6, 1, 6, 2);
            focus.VerticalAlignment = VerticalAlignment.Center;
            if (current) SetOn(focus, true);
            Grid.SetColumn(focus, 2);
            row.Children.Add(focus);
            var card = MenuWindow.Card(row, false);
            card.Padding = new Thickness(6, 4, 4, 4);
            card.Margin = new Thickness(0, 0, 0, 4);
            _list.Children.Add(card);
        }
    }

    private void BuildNotes()
    {
        var notes = _pet.Save.Notes;
        _count.Text = notes.Count > 0 ? $"· {notes.Count}" : "";
        if (notes.Count == 0) _list.Children.Add(MenuWindow.MutedText("Тефтерът е празен."));
        foreach (var note in notes.ToList())
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // Само за четене, но се маркира и копира.
            grid.Children.Add(new TextBox
            {
                Text = n.Text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Ink,
                FontFamily = GameFont,
                FontSize = Body,
                Padding = new Thickness(0),
            });
            var buttons = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var copy = Chip("⧉", () => { try { Clipboard.SetText(n.Text); } catch (Exception) { } });
            copy.ToolTip = Tip("Копирай");
            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.ToolTip = Tip("Изтрий");
            foreach (var b in new[] { copy, del })
            {
                b.Padding = new Thickness(5, 0, 5, 1);
                b.Margin = new Thickness(4, 0, 0, 3);
                buttons.Children.Add(b);
            }
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            var card = MenuWindow.Card(grid, false);
            card.Padding = new Thickness(6, 4, 4, 4);
            card.Margin = new Thickness(0, 0, 0, 4);
            _list.Children.Add(card);
        }
    }
}
