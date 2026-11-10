using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class PresenterWindow : Window
{
    private readonly PresentationWindow _show;
    private readonly ITextCatalog _texts;

    private readonly SlideView _current;
    private readonly SlideView _next;
    private readonly TextBlock _notes = new() { TextWrapping = TextWrapping.Wrap, FontSize = 18 };
    private readonly TextBlock _elapsed = new() { FontSize = 30, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _slideClock = new() { FontSize = 16, Opacity = 0.75 };
    private readonly TextBlock _counter = new() { FontSize = 16, Opacity = 0.75 };
    private readonly TextBlock _nextLabel = new() { FontSize = 13, Opacity = 0.6 };

    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly DispatcherTimer _timer;
    private TimeSpan _slideStart = TimeSpan.Zero;
    private Slide? _shownSlide;
    private readonly Presentation _deck;
    private readonly ShowSlideSetup _setup = new();

    public PresenterWindow(Presentation deck, ISlideRenderer renderer, PresentationWindow show, ITextCatalog texts)
    {
        _show = show;
        _texts = texts;
        _deck = deck;

        _current = new SlideView(renderer) { Size = deck.Size, DeckTheme = deck.Theme, Master = deck.Master, Deck = deck, Padding = 0 };
        _next = new SlideView(renderer) { Size = deck.Size, DeckTheme = deck.Theme, Master = deck.Master, Deck = deck, Padding = 0 };

        Title = texts["deck.presenter.title"];
        Background = new SolidColorBrush(Color.FromRgb(18, 18, 22));
        Width = 1100;
        Height = 680;
        Content = BuildLayout();

        Tint(_notes, _elapsed, _slideClock, _counter, _nextLabel);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        _show.SlideChanged += OnSlideChanged;
        KeyDown += OnKeyDown;
        Opened += (_, _) => { Refresh(); Focus(); };
    }

    private Control BuildLayout()
    {
        var currentFrame = Framed(_current);
        var nextFrame = Framed(_next);

        var prev = NavButton("‹", _texts["deck.presenter.previousStep"], _show.BackExternal);
        var advance = NavButton("›", _texts["deck.presenter.nextStep"], _show.AdvanceExternal);

        var timerBox = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2,
            Children = { _elapsed, _slideClock },
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
        Grid.SetColumn(timerBox, 0);
        var spacer = new Control();
        Grid.SetColumn(spacer, 1);
        Grid.SetColumn(_counter, 2);
        Grid.SetColumn(prev, 3);
        Grid.SetColumn(advance, 4);
        _counter.VerticalAlignment = VerticalAlignment.Center;
        _counter.Margin = new Thickness(0, 0, 12, 0);
        header.Children.Add(timerBox);
        header.Children.Add(spacer);
        header.Children.Add(_counter);
        header.Children.Add(prev);
        header.Children.Add(advance);

        var notesTitle = new TextBlock
        {
            Text = _texts["deck.presenter.notes"],
            FontSize = 13,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 6),
        };
        Tint(notesTitle);

        var notesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { notesTitle, _notes },
        };

        var rightColumn = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,2*") };
        Grid.SetRow(_nextLabel, 0);
        Grid.SetRow(nextFrame, 1);
        var notesScroll = new ScrollViewer { Content = notesPanel };
        Grid.SetRow(notesScroll, 3);
        _nextLabel.Margin = new Thickness(0, 0, 0, 6);
        rightColumn.Children.Add(_nextLabel);
        rightColumn.Children.Add(nextFrame);
        rightColumn.Children.Add(notesScroll);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,*"),
            Margin = new Thickness(20, 12, 20, 20),
        };
        Grid.SetColumn(currentFrame, 0);
        currentFrame.Margin = new Thickness(0, 0, 20, 0);
        Grid.SetColumn(rightColumn, 1);
        body.Children.Add(currentFrame);
        body.Children.Add(rightColumn);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(20, 16, 20, 0) };
        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        root.Children.Add(header);
        root.Children.Add(body);
        return root;
    }

    private static Border Framed(Control child) => new()
    {
        Background = Brushes.Black,
        Child = child,
        CornerRadius = new CornerRadius(4),
        ClipToBounds = true,
    };

    private Button NavButton(string glyph, string name, Action action)
    {
        var button = new Button
        {
            Content = glyph,
            FontSize = 22,
            Width = 46,
            Height = 40,
            Margin = new Thickness(4, 0, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        button.Click += (_, _) => { action(); Focus(); };
        return button;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right or Key.Down or Key.Space or Key.Enter or Key.PageDown:
                _show.AdvanceExternal();
                break;
            case Key.Left or Key.Up or Key.PageUp or Key.Back:
                _show.BackExternal();
                break;
            case Key.Home:
                _show.FirstExternal();
                break;
            case Key.End:
                _show.LastExternal();
                break;
            case Key.Escape:
                _show.Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnSlideChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(_show.CurrentSlide, _shownSlide)) _slideStart = _clock.Elapsed;
        Refresh();
    }

    private void Refresh()
    {
        _shownSlide = _show.CurrentSlide;
        _setup.Show(_current, _deck, _show.CurrentSlide);
        _setup.Show(_next, _deck, _show.NextSlide);
        _notes.Text = string.IsNullOrWhiteSpace(_show.CurrentSlide?.Notes)
            ? _texts["deck.presenter.noNotes"]
            : _show.CurrentSlide!.Notes;
        _nextLabel.Text = _show.NextSlide is null
            ? _texts["deck.presenter.end"]
            : _texts["deck.presenter.next"];
        _counter.Text = $"{_show.SlideNumber} / {_show.SlideTotal}";
    }

    private void Tick()
    {
        _elapsed.Text = Format(_clock.Elapsed);
        _slideClock.Text = Format(_clock.Elapsed - _slideStart);
    }

    private static string Format(TimeSpan span) => $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";

    private void Tint(params TextBlock[] blocks)
    {
        var brush = new SolidColorBrush(Color.FromRgb(236, 238, 244));
        foreach (var block in blocks) block.Foreground = brush;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _show.SlideChanged -= OnSlideChanged;
        base.OnClosed(e);
    }
}
