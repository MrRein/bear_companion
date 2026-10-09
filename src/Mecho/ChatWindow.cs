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
public sealed record ChatQuestion(string Text, params ChatOption[] Options);

/// <summary>
/// Голямо балонче над мечока с въпрос и бутони. Затваря се с избор, Esc или
/// клик другаде.
/// </summary>
public sealed class ChatWindow : Window
{
    private readonly PetWindow _pet;
    private readonly TextBlock _text;
    private readonly StackPanel _options = new();

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
        FontSize = 10;
        Foreground = Ink;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased); // пикселният шрифт да е рязък

        _text = new TextBlock
        {
            FontSize = 10,
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
        _text.Text = question.Text;
        _options.Children.Clear();
        foreach (var option in question.Options)
        {
            var o = option;
            var b = Button(o.Label, () =>
            {
                Hide();
                o.Do(_pet);
            });
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.MaxWidth = 340;
            _options.Children.Add(b);
        }

        if (!IsVisible) Show();
        UpdateLayout();
        double left = bear.Left + bear.Width / 2 - ActualWidth / 2;
        double top = bear.Top - ActualHeight - 6;
        Left = Math.Clamp(left, area.Left + 4, Math.Max(area.Left + 4, area.Right - ActualWidth - 4));
        Top = Math.Max(area.Top + 4, top);
        Activate();
    }
}
