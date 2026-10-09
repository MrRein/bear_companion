using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Режим „Рисувай“: върху целия екран се рисува с четка (ляв бутон) и се трие
/// (десен бутон или гума). Мечокът стъпва по нарисуваното. Esc или „Готово“ излиза.
/// </summary>
public sealed class DrawWindow : Window
{
    private readonly PetWindow _pet;
    private readonly DrawingCanvas _canvas;
    private readonly ToolbarWindow _toolbar;
    private byte _color = 1;
    private int _size = 4;
    private bool _eraser;
    private bool _drawing, _erasingWithRight;
    private int _lastCol = int.MinValue, _lastRow;

    public DrawWindow(PetWindow pet, DrawingCanvas canvas)
    {
        _pet = pet;
        _canvas = canvas;
        Title = "Рисуване";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        // Почти прозрачен фон, за да хваща мишката по целия екран.
        Background = new SolidColorBrush(Color.FromArgb(18, 255, 244, 220));
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = canvas.Area.Left;
        Top = canvas.Area.Top;
        Width = canvas.Area.Width;
        Height = canvas.Area.Height;
        Cursor = Cursors.Pen;

        _toolbar = new ToolbarWindow(this);
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
        MouseLeftButtonDown += (_, e) => Begin(e, right: false);
        MouseRightButtonDown += (_, e) => Begin(e, right: true);
        MouseMove += (_, e) => { if (_drawing) Paint(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, _) => End();
        MouseRightButtonUp += (_, _) => End();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closed += (_, _) =>
        {
            _toolbar.Close();
            _pet.DrawingClosed(_canvas);
        };
    }

    public void Open()
    {
        _canvas.ShowLayer(true);
        Show();
        _toolbar.Owner = this;
        _toolbar.Show();
        _toolbar.UpdateLayout();
        _toolbar.Left = _canvas.Area.Left + (_canvas.Area.Width - _toolbar.ActualWidth) / 2;
        _toolbar.Top = _canvas.Area.Top + 12;
        Activate();
    }

    private void Begin(MouseButtonEventArgs e, bool right)
    {
        _drawing = true;
        _erasingWithRight = right;
        _lastCol = int.MinValue;
        CaptureMouse();
        Paint(e.GetPosition(this));
        e.Handled = true;
    }

    private void End()
    {
        if (!_drawing) return;
        _drawing = false;
        ReleaseMouseCapture();
        _canvas.Save(DrawingStore.Folder);
    }

    /// <summary>Рисува от последната точка до новата, за да няма дупки при бързо движение.</summary>
    private void Paint(Point p)
    {
        int col = _canvas.Col(_canvas.Area.Left + p.X), row = _canvas.Row(_canvas.Area.Top + p.Y);
        byte color = _eraser || _erasingWithRight ? (byte)0 : _color;
        int size = _erasingWithRight && !_eraser ? Math.Max(_size, 6) : _size;
        if (_lastCol == int.MinValue)
            _canvas.Stamp(col, row, size, color);
        else
        {
            int steps = Math.Max(Math.Abs(col - _lastCol), Math.Abs(row - _lastRow));
            for (int i = 1; i <= Math.Max(1, steps); i++)
            {
                int x = _lastCol + (col - _lastCol) * i / Math.Max(1, steps);
                int y = _lastRow + (row - _lastRow) * i / Math.Max(1, steps);
                _canvas.Stamp(x, y, size, color);
            }
        }
        _lastCol = col;
        _lastRow = row;
        _canvas.ShowLayer(true);
    }

    // ───────────────────────── Палитрата ─────────────────────────

    private sealed class ToolbarWindow : Window
    {
        private readonly DrawWindow _owner;
        private readonly Border[] _swatches = new Border[DrawingCanvas.Palette.Length];
        private readonly Border[] _sizes = new Border[3];
        private Button _brush = null!, _erase = null!;
        private static readonly int[] SizeCells = { 2, 4, 8 };

        public ToolbarWindow(DrawWindow owner)
        {
            _owner = owner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = GameFont;
            FontSize = Body;
            Foreground = Ink;
            UseLayoutRounding = true;
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
            Cursor = Cursors.Arrow;
            KeyDown += (_, e) => { if (e.Key == Key.Escape) _owner.Close(); };

            var panel = new StackPanel();

            // Заглавие: дръжка за местене.
            var header = new DockPanel { Background = Brushes.Transparent, Cursor = Cursors.SizeAll, Margin = new Thickness(0, 0, 0, 8) };
            header.MouseLeftButtonDown += (_, _) =>
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            };
            var done = Button("Готово", _owner.Close, primary: true);
            done.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(done, Dock.Right);
            header.Children.Add(done);
            header.Children.Add(IconText("pencil", "Рисувай платформи за мечока", bold: true));
            panel.Children.Add(header);

            // Цветове
            var colors = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            for (byte i = 1; i < DrawingCanvas.Palette.Length; i++)
            {
                byte index = i;
                var swatch = new Border
                {
                    Width = Body * 1.6,
                    Height = Body * 1.6,
                    Margin = new Thickness(0, 0, 5, 0),
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = Ink,
                    BorderThickness = new Thickness(2),
                    Background = new SolidColorBrush(DrawingCanvas.Palette[i]),
                    Cursor = Cursors.Hand,
                };
                swatch.MouseLeftButtonDown += (_, e) => { e.Handled = true; };
                swatch.MouseLeftButtonUp += (_, e) =>
                {
                    _owner._color = index;
                    _owner._eraser = false;
                    Update();
                    e.Handled = true;
                };
                _swatches[i] = swatch;
                colors.Children.Add(swatch);
            }
            panel.Children.Add(colors);

            // Четка, гума, размер
            var tools = new WrapPanel();
            _brush = Button("Четка", () => { _owner._eraser = false; Update(); }, icon: "pencil");
            _erase = Button("Гума", () => { _owner._eraser = true; Update(); }, icon: "stop");
            tools.Children.Add(_brush);
            tools.Children.Add(_erase);
            string[] sizeNames = { "Тънка", "Средна", "Дебела" };
            for (int i = 0; i < 3; i++)
            {
                int s = SizeCells[i];
                _sizes[i] = Chip(sizeNames[i], () => { _owner._size = s; Update(); });
                _sizes[i].Margin = new Thickness(0, 0, 5, 6);
                _sizes[i].VerticalAlignment = VerticalAlignment.Center;
                tools.Children.Add(_sizes[i]);
            }
            panel.Children.Add(tools);

            var actions = new WrapPanel();
            var recolor = Button("Пребоядисай всичко", () => _owner._canvas.Recolor(_owner._color));
            recolor.ToolTip = Tip("Цялата рисунка става в избрания цвят.");
            var clear = Button("Изтрий всичко", () =>
            {
                _owner._canvas.Clear();
                _owner._canvas.Save(DrawingStore.Folder);
            });
            actions.Children.Add(recolor);
            actions.Children.Add(clear);
            panel.Children.Add(actions);

            panel.Children.Add(new TextBlock
            {
                Text = "Ляв бутон рисува, десен трие. Мечокът стъпва по нарисуваното. Esc излиза.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = Body * 26,
            });

            Content = Frame(panel, new Thickness(12, 10, 12, 10));
            Update();
        }

        private void Update()
        {
            for (int i = 1; i < _swatches.Length; i++)
            {
                bool on = i == _owner._color && !_owner._eraser;
                _swatches[i].BorderThickness = new Thickness(on ? 4 : 2);
                _swatches[i].BorderBrush = on ? Honey : Ink;
            }
            for (int i = 0; i < 3; i++) _sizes[i].Background = SizeCells[i] == _owner._size ? Honey : PaperDark;
            _brush.Background = _owner._eraser ? PaperDark : Honey;
            _erase.Background = _owner._eraser ? Honey : PaperDark;
        }
    }
}
