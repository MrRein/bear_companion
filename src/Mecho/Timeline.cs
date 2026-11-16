using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Mecho.Core;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Графикът по часове (като календара в Sunsama) за един или няколко дни:
/// * задачите с час (Start) са блокчета – влачиш ги нагоре/надолу (и в друг ден), разтягаш
///   ги за долния ръб (колко време), цъкнеш – картата на задачата, десен бутон – още;
/// * задача от списъка се пуска направо в часа (влачене);
/// * двойно цъкване на празно място – нова задача в този час;
/// * червена черта – сега; всичко се нарежда на 15 минути.
/// </summary>
internal sealed class DayTimeline : DockPanel
{
    private const int Snap = 15;
    private static readonly CultureInfo Bg = new("bg-BG");
    private static double U => Body / 18;
    /// <summary>Колко е висок един час: расте с прозореца (поне 46, иначе ~1/11 от видимото).</summary>
    private double HourH => _hourH > 0 ? _hourH : 46 * U;
    private double _hourH;
    private const double LabelW = 44;

    private readonly PetWindow _pet;
    private readonly Action<TaskItem> _open;
    private readonly bool _headers;
    private readonly ScrollViewer _scroll;
    private readonly Grid _head = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(30) };
    private List<DateTime> _days = new() { DateTime.Today };
    private Canvas[] _canvases = Array.Empty<Canvas>();
    private Grid _body = new();
    private string _shown = "";
    private bool _placed, _placeWired;

    // Влачене / разтягане на блокче.
    private enum Mode { None, Pending, Move, Resize }
    private Mode _mode;
    private TaskItem? _task;
    private Border? _block;
    private Point _from;
    private double _origTop, _origHeight;
    private int _origDay;
    private TextBlock? _timeLabel;
    private Border? _dropGhost;
    private bool _reparenting;

    public DayTimeline(PetWindow pet, Action<TaskItem> open, bool headers)
    {
        _pet = pet;
        _open = open;
        _headers = headers;
        LastChildFill = true;
        _head.Margin = new Thickness(LabelW * U, 0, 0, 0);
        DockPanel.SetDock(_head, Dock.Top);
        if (headers) Children.Add(_head);
        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
        };
        // Черта над часовете: като се превърта, графикът се реже на нея, а не насред нещо.
        var rule = new Border { Height = Math.Max(1, P / 2), Background = WoodDark, Margin = new Thickness(0, 4 * U, 0, 0) };
        DockPanel.SetDock(rule, Dock.Top);
        Children.Add(rule);
        Children.Add(_scroll);
        _tick.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { _tick.Start(); Refresh(true); }
            else _tick.Stop();
        };
        _pet.CareChanged += (_, _) => { if (IsVisible && _mode == Mode.None) Refresh(); };
        SizeChanged += (_, _) => LayoutBlocks();
        _scroll.SizeChanged += (_, _) =>
        {
            double h = Math.Max(46 * U, Math.Min(90 * U, _scroll.ViewportHeight / 11));
            if (Math.Abs(h - HourH) < 2 || _mode != Mode.None) return;
            double hours = _scroll.VerticalOffset / HourH;
            _hourH = h;
            _shown = "";
            Refresh(true);
            Dispatcher.BeginInvoke(() => _scroll.ScrollToVerticalOffset(hours * HourH), DispatcherPriority.Loaded);
        };
    }

    /// <summary>Кои дни се показват (един – „Днес“; седем – „Календар“).</summary>
    public void SetDays(IEnumerable<DateTime> days)
    {
        var list = days.Select(d => d.Date).ToList();
        if (list.SequenceEqual(_days)) return;
        _days = list;
        _shown = "";
        Refresh(true);
    }

    public IReadOnlyList<DateTime> Days => _days;

    /// <summary>Самопроверката: колко блокчета има в графика.</summary>
    internal int BlockCount => _canvases.Sum(c => c.Children.OfType<Border>().Count(b => b.Tag is TaskItem));

    private IEnumerable<TaskItem> Scheduled(DateTime day)
    {
        var state = _pet.TaskState;
        return state.Open.Concat(state.History).Where(t => t.Start is { } s && s.Date == day);
    }

    /// <summary>Несвършените задачи от деня без час (показват се бледо – предложено време).</summary>
    private IEnumerable<TaskItem> Unscheduled(DateTime day) =>
        day < _pet.TaskToday ? Enumerable.Empty<TaskItem>() : _pet.TaskState.Open.Where(t => t.Day == day && t.Start == null && !t.Done);

    /// <summary>
    /// Предложеното време за задачите без час: една след друга от сега (днес) или от 9:00
    /// (следващите дни), без да се застъпват със сложените в графика.
    /// </summary>
    private List<(TaskItem Task, DateTime Start)> Projected(DateTime day)
    {
        var busy = Scheduled(day).Where(t => !t.Done).Select(t => (From: t.Start!.Value, To: t.Start!.Value.AddMinutes(Length(t)))).OrderBy(x => x.From).ToList();
        var now = DateTime.Now;
        var at = day == _pet.TaskToday
            ? now.Date.AddHours(now.Hour).AddMinutes((now.Minute / Snap + 1) * Snap)
            : day.AddHours(9);
        var list = new List<(TaskItem, DateTime)>();
        foreach (var t in Unscheduled(day))
        {
            int len = Length(t);
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach (var b in busy)
                    if (at < b.To && at.AddMinutes(len) > b.From) { at = b.To; moved = true; }
            }
            if (at.Date != day) break;   // не се събира до края на деня
            list.Add((t, at));
            at = at.AddMinutes(len);
        }
        return list;
    }

    public void Refresh(bool force = false)
    {
        if (!IsVisible && !force) return;
        if (_mode != Mode.None || TaskCards.Dragging) return;
        var now = DateTime.Now;
        string sig = string.Join(",", _days.Select(d => d.ToString("yyyyMMdd"))) + $"#{now:yyyyMMddHHmm}#{_pet.Save.UiTheme}#{TaskTags.Version}#"
            + string.Join("|", _days.SelectMany(Scheduled).Select(t => t.Signature + t.Done))
            + "#u:" + string.Join("|", _days.SelectMany(Unscheduled).Select(t => t.Signature));
        if (!force && sig == _shown) return;
        _shown = sig;
        Build();
    }

    // ── Строене ──

    private void Build()
    {
        double offset = _scroll.VerticalOffset;
        int n = _days.Count;
        var today = _pet.TaskToday;

        // Заглавия на дните (в „Календар“).
        _head.Children.Clear();
        _head.ColumnDefinitions.Clear();
        for (int i = 0; i < n; i++)
        {
            _head.ColumnDefinitions.Add(new ColumnDefinition());
            var d = _days[i];
            string name = d.ToString("ddd d", Bg);
            var t = new TextBlock
            {
                Text = char.ToUpper(name[0], Bg) + name[1..],
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var cell = new Border
            {
                Child = t,
                Background = d == today ? Honey : Brushes.Transparent,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(0, 2, 0, 3),
            };
            Grid.SetColumn(cell, i);
            _head.Children.Add(cell);
        }

        _body = new Grid { Height = 24 * HourH + 8 * U, Background = Brushes.Transparent };
        _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelW * U) });
        for (int i = 0; i < n; i++) _body.ColumnDefinitions.Add(new ColumnDefinition());

        // Часовете вляво и чертите през всички дни.
        var labels = new Canvas();
        for (int h = 0; h <= 24; h++)
        {
            var label = new TextBlock { Text = $"{h % 24}:00", Foreground = Muted, FontSize = Body * 0.85 };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, h * HourH - 7 * U + 4 * U);
            if (h < 24) labels.Children.Add(label);
        }
        _body.Children.Add(labels);

        _ghostStart.Clear();
        _timeLabels.Clear();
        _canvases = new Canvas[n];
        for (int i = 0; i < n; i++)
        {
            int index = i;
            var day = _days[i];
            var canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true, Margin = new Thickness(0, 4 * U, 0, 0), AllowDrop = true, Tag = day };
            // Часовите черти (половината – по-бледа).
            for (int h = 0; h < 24; h++)
            {
                canvas.Children.Add(Line(h * HourH, WoodLight, 1));
                canvas.Children.Add(Line(h * HourH + HourH / 2, WoodLight, 0.45));
            }
            // Делниците – съвсем леко различен фон за нощните часове (преди 8 и след 20).
            var night1 = new Rectangle { Fill = PaperDark, Opacity = 0.45, Height = 8 * HourH, IsHitTestVisible = false };
            var night2 = new Rectangle { Fill = PaperDark, Opacity = 0.45, Height = 4 * HourH, IsHitTestVisible = false };
            Canvas.SetTop(night2, 20 * HourH);
            canvas.Children.Insert(0, night1);
            canvas.Children.Insert(0, night2);
            canvas.SizeChanged += (_, _) => { night1.Width = night2.Width = canvas.ActualWidth; LayoutBlocks(); };

            Grid.SetColumn(canvas, i + 1);
            var frame = new Border { BorderBrush = WoodLight, BorderThickness = new Thickness(1, 0, 0, 0), IsHitTestVisible = false, Margin = new Thickness(0, 4 * U, 0, 0) };
            Grid.SetColumn(frame, i + 1);
            _body.Children.Add(frame);
            _body.Children.Add(canvas);
            _canvases[i] = canvas;
            WireCanvas(canvas, index);

            foreach (var t in Scheduled(day).OrderBy(t => t.Start)) canvas.Children.Add(Block(t, t.Start!.Value, ghost: false));
            foreach (var (t, at) in Projected(day)) canvas.Children.Add(Block(t, at, ghost: true));

            // Сега.
            if (day == today)
            {
                var now = DateTime.Now;
                double y = (now - now.Date).TotalHours * HourH;
                var line = new Rectangle { Fill = Bad, Height = Math.Max(2, P / 2), IsHitTestVisible = false, Tag = "now" };
                var dot = new Ellipse { Fill = Bad, Width = 2.5 * P, Height = 2.5 * P, IsHitTestVisible = false, Tag = "now" };
                Canvas.SetTop(line, y - line.Height / 2);
                Canvas.SetTop(dot, y - dot.Height / 2);
                Canvas.SetLeft(dot, -dot.Width / 2);
                canvas.Children.Add(line);
                canvas.Children.Add(dot);
                canvas.SizeChanged += (_, _) => line.Width = canvas.ActualWidth;
            }
        }
        _scroll.Content = _body;
        LayoutBlocks();

        // Първия път – часът преди сега (когато графикът вече има размер); иначе – където е бил.
        if (_placed) Dispatcher.BeginInvoke(() => _scroll.ScrollToVerticalOffset(offset), DispatcherPriority.Loaded);
        else PlaceAtNow();
    }

    private void PlaceAtNow()
    {
        void Try()
        {
            if (_placed || _scroll.ViewportHeight <= 0 || _scroll.ExtentHeight <= 0) return;
            _placed = true;
            var now = DateTime.Now;
            _scroll.ScrollToVerticalOffset(Math.Max(0, ((now - now.Date).TotalHours - 1.2) * HourH));
        }
        if (!_placeWired)
        {
            _placeWired = true;
            _scroll.ScrollChanged += (_, _) => Try();
            _scroll.SizeChanged += (_, _) => Try();
        }
        Dispatcher.BeginInvoke(Try, DispatcherPriority.Loaded);
    }

    private Rectangle Line(double y, Brush brush, double opacity)
    {
        var r = new Rectangle { Fill = brush, Height = 1, Opacity = opacity, IsHitTestVisible = false, Width = 4000 };
        Canvas.SetTop(r, y);
        return r;
    }

    private double Y(DateTime t) => (t - t.Date).TotalHours * HourH;

    private static int Length(TaskItem t) => t.Minutes > 0 ? t.Minutes : 30;

    /// <summary>Блокче за задача: цвят на етикета, отметка, име, часът; долу – дръжка за дължината.</summary>
    private readonly Dictionary<Border, DateTime> _ghostStart = new();

    private Border Block(TaskItem t, DateTime start, bool ghost)
    {
        double height = Math.Max(Length(t) / 60.0 * HourH - 2, 14 * U);
        var tag = TaskTags.Find(t.Tag);
        var text = new StackPanel();
        var titleRow = new DockPanel();
        if (height >= 20 * U)
        {
            var check = TaskCards.CheckSquare(t.Done, on => _pet.SetTaskDone(t, on), enabled: !t.Done || _pet.Save.Tasks.Any(x => x.Id == t.Id));
            check.Width = check.Height = 4 * P;
            check.Margin = new Thickness(0, 1, 5 * U, 0);
            DockPanel.SetDock(check, Dock.Left);
            titleRow.Children.Add(check);
        }
        titleRow.Children.Add(new TextBlock
        {
            Text = t.Title,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
        });
        text.Children.Add(titleRow);
        var time = new TextBlock { Text = Range(start, Length(t)), Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis };
        if (height >= 34 * U) text.Children.Add(time);
        var handle = new Border { Height = 6 * U, Background = Brushes.Transparent, Cursor = Cursors.SizeNS, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -2) };
        var grid = new Grid();
        grid.Children.Add(text);
        grid.Children.Add(handle);
        var block = new Border
        {
            Height = height,
            Background = tag != null ? TaskTags.BrushOf(t.Tag, 95) : Cream,
            BorderBrush = tag != null ? TaskTags.BrushOf(t.Tag) : WoodDark,
            BorderThickness = new Thickness(Math.Max(2, P), 1, 1, 1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5 * U, 2 * U, 4 * U, 0),
            Child = grid,
            ClipToBounds = true,
            Cursor = Cursors.Hand,
            Opacity = t.Done ? 0.55 : ghost ? 0.6 : 1,
            Tag = t,
            ToolTip = Tip($"{t.Title}\n{Range(start, Length(t))}" + (t.Minutes == 0 ? " (без казано време – 30 мин)" : "")
                + (ghost ? "\nПредложено време – хвани и пусни, за да я сложиш в графика." : "")),
        };
        if (ghost)
        {
            // Предложените – с пунктир (рисуван) и по-бледи.
            block.BorderBrush = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 6, 6),
                ViewportUnits = BrushMappingMode.Absolute,
                Drawing = new GeometryDrawing(tag != null ? TaskTags.BrushOf(t.Tag) : WoodDark, null, new RectangleGeometry(new Rect(0, 0, 3, 3))),
            };
            block.Background = tag != null ? TaskTags.BrushOf(t.Tag, 45) : Paper;
            time.Text = "~" + time.Text;
            _ghostStart[block] = start;
        }
        Canvas.SetTop(block, Y(start) + 1);
        _timeLabels[block] = time;

        block.MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled) return;
            e.Handled = true;
            Begin(block, t, e.GetPosition(_body), handle.IsMouseOver ? Mode.Resize : Mode.Pending);
        };
        block.MouseMove += (_, e) => DragTo(e.GetPosition(_body), e.GetPosition(_scroll).Y);
        block.MouseLeftButtonUp += (_, e) =>
        {
            if (_mode == Mode.None) return;
            e.Handled = true;
            Finish();
        };
        block.LostMouseCapture += (_, _) => { if (_mode != Mode.None && !_reparenting) Cancel(); };
        block.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            PixelPopup.Show(block, null, () => BlockEntries(t), atMouse: true);
        };
        return block;
    }

    private readonly Dictionary<Border, TextBlock> _timeLabels = new();

    private static string Range(DateTime start, int minutes) => $"{start:H:mm}–{start.AddMinutes(minutes):H:mm}";

    private IEnumerable<PixelPopup.Entry> BlockEntries(TaskItem t)
    {
        var list = new List<PixelPopup.Entry> { new("Отвори", "pencil", () => _open(t)) };
        if (!t.Done)
        {
            list.Add(new("Колко време", "hourglass", null, Sub: TaskCards.TimeEntries(_pet, t)));
            list.Add(new("Свършена", "check", () => _pet.SetTaskDone(t, true)));
        }
        list.Add(new("Махни от графика", "close", () => _pet.ScheduleTask(t, null)));
        return list;
    }

    /// <summary>Застъпващите се блокчета – едно до друго (колкото са, на равни части).</summary>
    private void LayoutBlocks()
    {
        foreach (var canvas in _canvases)
        {
            double w = canvas.ActualWidth;
            if (w <= 0) continue;
            var blocks = canvas.Children.OfType<Border>().Where(b => b.Tag is TaskItem).Select(b => (Block: b, Top: Canvas.GetTop(b), Bottom: Canvas.GetTop(b) + b.Height))
                .OrderBy(x => x.Top).ToList();
            int i = 0;
            while (i < blocks.Count)
            {
                // Група: всички, които се застъпват (верижно).
                int j = i;
                double end = blocks[i].Bottom;
                while (j + 1 < blocks.Count && blocks[j + 1].Top < end - 1)
                {
                    j++;
                    end = Math.Max(end, blocks[j].Bottom);
                }
                var lanes = new List<double>();
                var lane = new int[j - i + 1];
                for (int k = i; k <= j; k++)
                {
                    int l = lanes.FindIndex(b => b <= blocks[k].Top + 1);
                    if (l < 0) { lanes.Add(blocks[k].Bottom); l = lanes.Count - 1; }
                    else lanes[l] = blocks[k].Bottom;
                    lane[k - i] = l;
                }
                double laneW = (w - 6) / lanes.Count;
                for (int k = i; k <= j; k++)
                {
                    var b = blocks[k].Block;
                    b.Width = Math.Max(20, laneW - 3);
                    Canvas.SetLeft(b, 3 + lane[k - i] * laneW);
                }
                i = j + 1;
            }
        }
    }

    // ── Влачене на блокче ──

    private void Begin(Border block, TaskItem t, Point from, Mode mode, bool capture = true)
    {
        if (t.Done && mode == Mode.Resize) return;
        _mode = mode;
        _task = t;
        _block = block;
        _from = from;
        _origTop = Canvas.GetTop(block);
        _origHeight = block.Height;
        _origDay = Array.IndexOf(_canvases, block.Parent as Canvas);
        _timeLabels.TryGetValue(block, out _timeLabel);
        if (capture) block.CaptureMouse();
    }

    private double SnapY(double y) => Math.Round(y / (HourH * Snap / 60.0)) * (HourH * Snap / 60.0);

    /// <summary>Мишката е в p (в графика); scrollY – колко е от горния ръб на видимото (за превъртане до ръба).</summary>
    private void DragTo(Point p, double scrollY)
    {
        if (_mode == Mode.None || _block == null || _task == null) return;
        double dy = p.Y - _from.Y, dx = p.X - _from.X;
        if (_mode == Mode.Pending)
        {
            if (Math.Abs(dy) < 5 && Math.Abs(dx) < 5) return;
            if (_task.Done) return;   // свършените не се местят
            _mode = Mode.Move;
            _block.Opacity = 0.85;
            Panel.SetZIndex(_block, 50);
        }
        if (_mode == Mode.Move)
        {
            double top = Math.Clamp(SnapY(_origTop - 1 + dy), 0, 24 * HourH - _block.Height) + 1;
            Canvas.SetTop(_block, top);
            // В друг ден (в „Календар“).
            int day = DayAt(p.X);
            if (day >= 0 && _block.Parent is Canvas from && from != _canvases[day])
            {
                // Махането от дървото губи „хванатата“ мишка – хваща се пак.
                _reparenting = true;
                from.Children.Remove(_block);
                _canvases[day].Children.Add(_block);
                _block.Width = Math.Max(20, _canvases[day].ActualWidth - 6);
                Canvas.SetLeft(_block, 3);
                if (Mouse.LeftButton == MouseButtonState.Pressed) _block.CaptureMouse();
                _reparenting = false;
            }
            if (_timeLabel != null) _timeLabel.Text = Range(TimeAt(DayOf(_block), top - 1), Length(_task));
            AutoScroll(scrollY);
        }
        else if (_mode == Mode.Resize)
        {
            double h = Math.Max(HourH * Snap / 60.0, SnapY(_origHeight + 2 + dy) - 2);
            _block.Height = Math.Min(h, 24 * HourH - Canvas.GetTop(_block));
            int minutes = (int)Math.Round((_block.Height + 2) / HourH * 60 / Snap) * Snap;
            var from = _task.Start ?? (_ghostStart.TryGetValue(_block, out var g) ? g : DateTime.Today);
            if (_timeLabel != null) _timeLabel.Text = Range(from, minutes);
            AutoScroll(scrollY);
        }
    }

    private void Finish()
    {
        if (_mode == Mode.None || _block == null || _task == null) return;
        var mode = _mode;
        var t = _task;
        var block = _block;
        _mode = Mode.None;
        _task = null;
        _block = null;
        block.ReleaseMouseCapture();
        if (mode == Mode.Pending)
        {
            _open(t);
            return;
        }
        if (mode == Mode.Move)
        {
            var start = TimeAt(DayOf(block), Canvas.GetTop(block) - 1);
            if (start != t.Start) _pet.ScheduleTask(t, start);
            else Refresh(true);
        }
        else if (mode == Mode.Resize)
        {
            int minutes = (int)Math.Round((block.Height + 2) / HourH * 60 / Snap) * Snap;
            minutes = Math.Clamp(minutes, Snap, TasksModule.MaxMinutes);
            var at = t.Start ?? (_ghostStart.TryGetValue(block, out var g) ? g : (DateTime?)null);
            if (minutes != t.Minutes || t.Start == null) _pet.ScheduleTask(t, at, minutes);
            else Refresh(true);
        }
        _shown = "";
        Refresh(true);
    }

    private void Cancel()
    {
        var block = _block;
        _mode = Mode.None;
        _task = null;
        _block = null;
        block?.ReleaseMouseCapture();
        _shown = "";
        Refresh(true);
    }

    private DateTime DayOf(Border block) => block.Parent is Canvas { Tag: DateTime d } ? d : _days[0];

    private DateTime TimeAt(DateTime day, double y)
    {
        int minutes = (int)Math.Round(y / HourH * 60 / Snap) * Snap;
        return day.AddMinutes(Math.Clamp(minutes, 0, 24 * 60 - Snap));
    }

    private int DayAt(double x)
    {
        double left = LabelW * U, w = (_body.ActualWidth - left) / Math.Max(1, _days.Count);
        if (w <= 0) return -1;
        return Math.Clamp((int)((x - left) / w), 0, _days.Count - 1);
    }

    private void AutoScroll(double y)
    {
        if (double.IsNaN(y)) return;
        double edge = 26 * U;
        if (y < edge) _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset - 10 * U);
        else if (y > _scroll.ActualHeight - edge) _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset + 10 * U);
    }

    // ── Празното място: пускане на задача от списъка, нова задача ──

    private void WireCanvas(Canvas canvas, int index)
    {
        canvas.DragOver += (_, e) =>
        {
            e.Handled = true;
            if (TaskCards.DraggedId(e) is not { } id || _pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task)
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            e.Effects = DragDropEffects.Move;
            double y = SnapY(e.GetPosition(canvas).Y - HourH / 4);
            double h = Length(task) / 60.0 * HourH - 2;
            if (_dropGhost == null)
            {
                _dropGhost = new Border
                {
                    Background = Hover,
                    BorderBrush = Ink,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    IsHitTestVisible = false,
                    Opacity = 0.8,
                    Child = new TextBlock { Margin = new Thickness(5 * U, 2 * U, 0, 0), FontWeight = FontWeights.Bold },
                };
            }
            if (_dropGhost.Parent != canvas)
            {
                (_dropGhost.Parent as Canvas)?.Children.Remove(_dropGhost);
                canvas.Children.Add(_dropGhost);
            }
            _dropGhost.Width = Math.Max(20, canvas.ActualWidth - 6);
            _dropGhost.Height = Math.Max(14 * U, h);
            Canvas.SetLeft(_dropGhost, 3);
            Canvas.SetTop(_dropGhost, Math.Max(0, y) + 1);
            ((TextBlock)_dropGhost.Child).Text = Range(TimeAt(_days[index], Math.Max(0, y)), Length(task)) + "  " + task.Title;
            AutoScroll(e.GetPosition(_scroll).Y);
        };
        canvas.DragLeave += (_, e) =>
        {
            var p = e.GetPosition(canvas);
            if (p.X < 0 || p.Y < 0 || p.X >= canvas.ActualWidth || p.Y >= canvas.ActualHeight) RemoveGhost();
        };
        canvas.Drop += (_, e) =>
        {
            e.Handled = true;
            double y = SnapY(e.GetPosition(canvas).Y - HourH / 4);
            RemoveGhost();
            if (TaskCards.DraggedId(e) is not { } id || _pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
            DropAt(task, index, y);
        };
        canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2 || e.OriginalSource != canvas) return;
            e.Handled = true;
            var start = TimeAt(_days[index], SnapY(e.GetPosition(canvas).Y - HourH * Snap / 120.0));
            NewTaskWindow.Show(_pet, _days[index], near: Window.GetWindow(this), start: start);
        };
        canvas.ToolTip = null;
    }

    /// <summary>Задача, пусната в ден index на височина y – в графика от този час.</summary>
    private void DropAt(TaskItem task, int index, double y)
    {
        var start = TimeAt(_days[index], Math.Max(0, y));
        _pet.ScheduleTask(task, start, task.Minutes > 0 ? null : 30);
    }

    // ── За самопроверката: същото като с мишката, но с дадени разстояния ──

    private Border? BlockOf(Guid id) =>
        _canvases.SelectMany(c => c.Children.OfType<Border>()).FirstOrDefault(b => b.Tag is TaskItem t && t.Id == id);

    /// <summary>Хваща блокчето и го мести с minutes надолу (или разтяга долния ръб), после го пуска.</summary>
    internal bool TestDrag(Guid id, double minutes, bool resize)
    {
        UpdateLayout();
        if (BlockOf(id) is not { } block || block.Tag is not TaskItem t) return false;
        var from = block.TranslatePoint(new Point(10, resize ? block.Height - 2 : 6), _body);
        Begin(block, t, from, resize ? Mode.Resize : Mode.Pending, capture: false);
        DragTo(new Point(from.X, from.Y + minutes / 60.0 * HourH), double.NaN);
        Finish();
        return true;
    }

    /// <summary>Пуска задача (като от списъка) в деня index в hour:minute.</summary>
    internal void TestDrop(TaskItem task, int index, double hours) => DropAt(task, index, hours * HourH);

    /// <summary>Колко от блокчетата са предложени (задачи без час).</summary>
    internal int GhostCount => _ghostStart.Count;

    private void RemoveGhost()
    {
        if (_dropGhost?.Parent is Canvas c) c.Children.Remove(_dropGhost);
    }
}
