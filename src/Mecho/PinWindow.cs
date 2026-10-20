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
    private readonly TextBlock _count = new() { Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Border _collapse, _fade;
    private string _shown = "";

    private static double U => Body / 18;

    /// <summary>Отначало: бележките са по-дълги надолу (текстът им е повече).</summary>
    private double DefaultHeight => _kind == Notes ? 440 * U : 320 * U;

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
        Height = s.H > 0 ? s.H : DefaultHeight;
        var area = SystemParameters.WorkArea;
        // Първия път – в средата на екрана (после там, където Тут го е сложила).
        Left = double.IsNaN(s.X) ? area.Left + (area.Width - Width) / 2 : Math.Clamp(s.X, area.Left - Width + 60, area.Right - 60);
        Top = double.IsNaN(s.Y) ? area.Top + Math.Max(40, (area.Height - Height) / 2) : Math.Clamp(s.Y, area.Top, area.Bottom - 40);

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
        // На дървената рамка – бели букви.
        System.Windows.Documents.TextElement.SetForeground(title, Brushes.White);
        _count.Foreground = Brushes.White;
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

        // „➕ Нова“ отваря менюто на новата задача/бележка (с всичко за нея – трудност, напомняне…).
        var newButton = Button("Нова", () => _pet.OpenNew(kind), primary: true, icon: "plus");
        newButton.Margin = new Thickness(0, 6, 0, 0);
        newButton.HorizontalAlignment = HorizontalAlignment.Stretch;

        _scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var bodyPanel = new DockPanel();
        DockPanel.SetDock(newButton, Dock.Bottom);
        bodyPanel.Children.Add(newButton);
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
            Height = height > 0 ? height : DefaultHeight;
        }
        _applying = false;
    }

    private bool _applying;

    /// <summary>Строи списъка наново, само ако нещо се е сменило.</summary>
    public void Refresh()
    {
        var s = _pet.Save;
        string sig = _kind == Tasks
            ? string.Join("|", s.Tasks.Where(t => !t.Done && _pet.TaskOnBoard(t)).Select(t => t.Id + t.Title + t.Description + t.Size)) + "#" + s.FocusTask + _pet.InFocus
            : string.Join("|", s.Notes.Where(n => n.Pinned).Select(n => n.Id + n.Title + "¦" + n.Text));
        if (sig == _shown) return;
        _shown = sig;
        _list.Children.Clear();
        if (_kind == Tasks) BuildTasks();
        else BuildNotes();
    }

    private void BuildTasks()
    {
        var s = _pet.Save;
        // Само задачите в таблото (📍 ги маха оттук, в менюто 📌 ги връща).
        var tasks = s.Tasks.Where(t => !t.Done && _pet.TaskOnBoard(t)).ToList();
        _count.Text = tasks.Count > 0 ? $"· {tasks.Count}" : "";
        if (tasks.Count == 0) _list.Children.Add(MenuWindow.MutedText("Няма задачи в таблото. Напиши една отдолу!"));
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
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(4, 0, 0, 0),
                },
            };
            check.Click += (_, _) => _pet.SetTaskDone(t, check.IsChecked == true);
            // Описанието (ако има) – под името, съкратено; цялото излиза при посочване.
            if (t.Description.Length > 0)
            {
                var both = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
                var head = (TextBlock)check.Content;
                head.Margin = new Thickness(0);
                check.Content = both;
                both.Children.Add(head);
                both.Children.Add(new TextBlock { Text = Ui.Shorten(t.Description, 70), Foreground = Muted, TextWrapping = TextWrapping.Wrap });
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
            var off = Chip("📍", () => _pet.SetTaskOnBoard(t, false));
            off.ToolTip = Tip("Махни я от таблото (задачата остава)");
            off.Padding = new Thickness(5, 1, 5, 2);
            off.Margin = new Thickness(4, 0, 0, 0);
            off.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(off, 2);
            row.Children.Add(off);
            var card = MenuWindow.Card(row, false);
            card.Padding = new Thickness(6, 4, 4, 4);
            card.Margin = new Thickness(0, 0, 0, 4);
            _list.Children.Add(card);
        }
    }

    private void BuildNotes()
    {
        // Само бележките с „📌 В таблото“.
        var notes = _pet.Save.Notes.Where(n => n.Pinned).ToList();
        _count.Text = notes.Count > 0 ? $"· {notes.Count}" : "";
        if (notes.Count == 0) _list.Children.Add(MenuWindow.MutedText("Няма закачени бележки."));
        foreach (var note in notes)
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // Само за четене, но се маркира и копира; името – удебелено.
            var text = new StackPanel();
            if (n.Title.Length > 0) text.Children.Add(new TextBlock { Text = n.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
            if (n.Text.Length > 0) text.Children.Add(new TextBox
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
            grid.Children.Add(text);
            var buttons = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var copy = Chip("⧉", () => { try { Clipboard.SetText(n.Title.Length > 0 && n.Text.Length > 0 ? n.Title + "\n" + n.Text : n.Title + n.Text); } catch (Exception) { } });
            copy.ToolTip = Tip("Копирай");
            var unpin = Chip("📍", () => _pet.SetNotePinned(n, false));
            unpin.ToolTip = Tip("Махни я от таблото (бележката остава)");
            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.ToolTip = Tip("Изтрий");
            foreach (var b in new[] { copy, unpin, del })
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
