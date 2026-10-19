using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>Един отговор в разговора: какво пише на бутона и какво става.</summary>
public sealed record ChatOption(string Label, Action<PetWindow> Do);

/// <summary>Въпрос (или реплика) на мечока с бутони за отговор.</summary>
public sealed record ChatQuestion(string Text, params ChatOption[] Options)
{
    /// <summary>Свободен отговор: поле, в което Тут пише своето (null = само бутони).</summary>
    public Action<PetWindow, string>? FreeText { get; init; }

    /// <summary>Надписът над полето и на бутона („Или напиши минути:“ / „Спи“).</summary>
    public string FreePrompt { get; init; } = "Или напиши своето:";
    public string FreeButton { get; init; } = "Отговори";
}

/// <summary>
/// Голямо балонче над мечока с въпрос и бутони. Затваря се с избор, Esc или
/// клик другаде.
/// </summary>
public sealed class ChatWindow : Window
{
    private readonly PetWindow _pet;
    private readonly TextBlock _text;
    private readonly StackPanel _options = new();
    private readonly TextBox _free = new()
    {
        MaxWidth = 340,
        MaxLength = 200,
        TextWrapping = TextWrapping.Wrap,
        Padding = new Thickness(4, 2, 4, 2),
        Margin = new Thickness(0, 4, 0, 6),
    };

    public ChatWindow(PetWindow pet)
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
        FontFamily = Ui.GameFont;
        FontSize = Ui.Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased); // пикселният шрифт да е рязък

        _text = new TextBlock
        {
            FontSize = Ui.Body,
            FontWeight = FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 340,
            Margin = new Thickness(0, 0, 0, 8),
        };
        var panel = new StackPanel { MinWidth = 200 };
        panel.Children.Add(_text);
        panel.Children.Add(_options);
        Content = Frame(panel, new Thickness(12, 10, 12, 8));

        Deactivated += (_, _) => Hide();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) _pet.ChatClosed();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Hide();
        };
    }

    /// <summary>Показва въпроса над мечока (в рамките на екрана).</summary>
    public void Ask(ChatQuestion question, Rect bear, Rect area)
    {
        Ui.SetRichText(_text, question.Text);
        _options.Children.Clear();
        foreach (var option in question.Options)
        {
            var o = option;
            // Познатите емоджи стават пикселни иконки, другите отпадат (виж Ui.SetRichText).
            string label = o.Label;
            var b = Button(label, () =>
            {
                Hide();
                o.Do(_pet);
            });
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.MaxWidth = 340;
            _options.Children.Add(b);
        }
        // Свободен отговор: поле и „Отговори“ (Enter също).
        if (question.FreeText is { } free)
        {
            _free.Text = "";
            _options.Children.Add(new TextBlock { Text = question.FreePrompt, Foreground = Muted, Margin = new Thickness(0, 6, 0, 0) });
            _options.Children.Add(_free);
            void Submit()
            {
                string text = _free.Text.Trim();
                if (text.Length == 0) return;
                Hide();
                free(_pet, text);
            }
            _free.KeyDown -= _freeKey;
            _freeKey = (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                Submit();
            };
            _free.KeyDown += _freeKey;
            var send = Button(question.FreeButton, Submit, primary: true);
            send.HorizontalAlignment = HorizontalAlignment.Right;
            _options.Children.Add(send);
        }

        // Първо се подрежда далеч от екрана и чак после застава над мечока –
        // иначе за миг се показваше на старото място (където мечокът беше преди).
        if (!IsVisible)
        {
            Left = -32000;
            Top = -32000;
            Show();
        }
        UpdateLayout();
        double left = bear.Left + bear.Width / 2 - ActualWidth / 2;
        double top = bear.Top - ActualHeight - 6;
        // Няма място отгоре (вдигнат е високо): под него.
        if (top < area.Top + 4) top = Math.Min(bear.Bottom + 6, area.Bottom - ActualHeight - 4);
        Left = Math.Clamp(left, area.Left + 4, Math.Max(area.Left + 4, area.Right - ActualWidth - 4));
        Top = Math.Max(area.Top + 4, top);
        Activate();
        if (question.FreeText != null) Dispatcher.BeginInvoke(() => _free.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private KeyEventHandler _freeKey = (_, _) => { };
}
