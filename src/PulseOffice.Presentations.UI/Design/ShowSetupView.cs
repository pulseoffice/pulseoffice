using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record ShowSetupResult(ShowSetup Setup, int MonitorIndex);

public sealed record ShowScreen(string Name, int Width, int Height, bool Primary);

public sealed record ShowSetupServices(IDeckSlidePictures Pictures, IMeasureUnits Units, IIconCatalog Icons, ITextCatalog Texts);

public sealed class ShowSetupView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double ThumbnailWidth = 96;

    private static readonly IReadOnlyList<(string Key, string Hex)> Pens =
    [
        ("red", "#E11D2E"), ("green", "#16A34A"), ("blue", "#2563EB"), ("amber", "#F59E0B"), ("white", "#FFFFFF"),
    ];

    private readonly ShowSetupServices _services;
    private readonly Presentation _deck;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly StackPanel _rangeCards = new() { Spacing = 6 };
    private readonly MeasureField _from;
    private readonly MeasureField _to;
    private readonly ContentControl _fromPicture = new();
    private readonly ContentControl _toPicture = new();
    private readonly Control _rangeRow;
    private readonly IconChoiceGroup _advance;
    private readonly CheckBox _loop = new();
    private readonly CheckBox _withoutAnimation = new();
    private readonly SwatchStrip _pen;
    private readonly ComboBox _screens = new() { MinWidth = 240 };
    private readonly int _initialMonitor;
    private bool _custom;

    public ShowSetupView(Presentation deck, int monitorIndex, ShowSetupServices services)
    {
        _deck = deck ?? throw new ArgumentNullException(nameof(deck));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _initialMonitor = monitorIndex;
        var setup = deck.Setup;
        var count = Math.Max(1, deck.Slides.Count);
        _custom = setup.HasRange;

        _from = SlideNumber(setup.FromSlide > 0 ? setup.FromSlide : 1, count, Texts["deck.setup.from"]);
        _to = SlideNumber(setup.ToSlide > 0 ? setup.ToSlide : count, count, Texts["deck.setup.to"]);
        _from.ValueCommitted += (_, _) => Refresh();
        _to.ValueCommitted += (_, _) => Refresh();
        _rangeRow = RangeRow();

        _advance = new IconChoiceGroup(
            [
                new IconChoice("timings", "deck.motion.advance.afterTime", Texts["deck.setup.advance.timings"]),
                new IconChoice("manual", "deck.motion.advance.onClick", Texts["deck.setup.advance.manual"]),
            ],
            services.Icons, _metrics, Texts["deck.setup.advance"], setup.Advance == ShowAdvanceMode.Manual ? 1 : 0, showLabels: true);
        _loop.Content = Texts["deck.setup.loop"];
        _loop.IsChecked = setup.Loop;
        _withoutAnimation.Content = Texts["deck.setup.noAnimation"];
        _withoutAnimation.IsChecked = setup.WithoutAnimation;

        var known = Pens.FirstOrDefault(pen => string.Equals(pen.Hex, setup.PenColor, StringComparison.OrdinalIgnoreCase));
        _pen = new SwatchStrip(
            [.. Pens.Select(pen => new Swatch(pen.Key, Texts[$"deck.setup.pen.{pen.Key}"], pen.Hex))],
            services.Texts, _metrics, Texts["deck.setup.pen"], known.Key);
        if (known.Key is null) _pen.Choose(setup.PenColor);

        AutomationProperties.SetName(_screens, Texts["deck.setup.screen"]);
        AttachedToVisualTree += (_, _) => LoadScreens(TopLevel.GetTopLevel(this)?.Screens?.All
            .Select((screen, index) => new ShowScreen(Texts.Format("deck.setup.screenName", index + 1), screen.Bounds.Width, screen.Bounds.Height, screen.IsPrimary))
            .ToList() ?? []);
        LoadScreens([]);

        var slides = new DialogSection(Texts["deck.setup.slides"], _metrics);
        slides.Add(null, _rangeCards, DialogRowKind.Wide);
        slides.Add(null, _rangeRow, DialogRowKind.Wide);
        var advance = new DialogSection(Texts["deck.setup.advance"], _metrics);
        advance.Add(null, _advance, DialogRowKind.Wide, editorWidth: 320);
        advance.Add(null, _loop, DialogRowKind.Indented);
        advance.Add(null, _withoutAnimation, DialogRowKind.Indented);
        var screen = new DialogSection(Texts["deck.setup.screens"], _metrics);
        screen.Add(Texts["deck.setup.screen"], _screens, editorWidth: 260);
        screen.Add(Texts["deck.setup.pen"], _pen, editorWidth: 260);

        var left = new StackPanel { Spacing = _metrics.SpacingLarge, Children = { slides } };
        var right = new StackPanel { Spacing = _metrics.SpacingLarge, Children = { advance, screen } };
        Grid.SetIsSharedSizeScope(right, true);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = _metrics.SpacingLarge * 2 };
        Grid.SetColumn(right, 1);
        layout.Children.Add(left);
        layout.Children.Add(right);
        Content = layout;

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithNote(Texts["deck.setup.explanation"])
            .WithAction(Texts["deck.motion.apply"], () => Completed?.Invoke(this, Result()), primary: true);
        BuildCards();
        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 760;

    public bool CustomRange => _custom;

    public MeasureField From => _from;

    public MeasureField To => _to;

    public SwatchStrip Pen => _pen;

    public ComboBox Screens => _screens;

    public IReadOnlyList<ShowScreen> ScreenList { get; private set; } = [];

    private ITextCatalog Texts => _services.Texts;

    public void ChooseRange(bool custom)
    {
        _custom = custom;
        BuildCards();
        Refresh();
    }

    public void LoadScreens(IReadOnlyList<ShowScreen> screens)
    {
        ScreenList = screens;
        var items = new List<string> { Texts["deck.setup.screenAuto"] };
        items.AddRange(screens.Select(screen => Texts.Format("deck.setup.screenEntry", screen.Name, screen.Width, screen.Height)
            + (screen.Primary ? " · " + Texts["deck.setup.primary"] : string.Empty)));
        var selected = _screens.SelectedIndex >= 0 ? _screens.SelectedIndex : _initialMonitor + 1;
        _screens.ItemsSource = items;
        _screens.SelectedIndex = Math.Clamp(selected, 0, items.Count - 1);
    }

    public ShowSetupResult Result()
    {
        var from = (int)Math.Round(_from.Value);
        var to = Math.Max(from, (int)Math.Round(_to.Value));
        return new ShowSetupResult(_deck.Setup with
        {
            FromSlide = _custom ? from : 0,
            ToSlide = _custom ? to : 0,
            Advance = _advance.SelectedIndex == 1 ? ShowAdvanceMode.Manual : ShowAdvanceMode.UseTimings,
            Loop = _loop.IsChecked == true,
            WithoutAnimation = _withoutAnimation.IsChecked == true,
            PenColor = _pen.Value ?? _deck.Setup.PenColor,
        }, Math.Max(0, _screens.SelectedIndex) - 1);
    }

    private void BuildCards()
    {
        _rangeCards.Children.Clear();
        foreach (var (custom, key, icon) in new[] { (false, "all", "deck.setup.rangeAll"), (true, "custom", "deck.setup.rangeCustom") })
        {
            var card = new ChoiceOptionCard(new PromptChoice(Texts[$"deck.setup.range.{key}"], custom, IsDefault: custom == _custom)
            {
                Description = Texts[$"deck.setup.range.{key}.description"],
                Icon = icon,
            }, _metrics, _services.Icons);
            card.Click += (_, _) => ChooseRange(custom);
            _rangeCards.Children.Add(card);
        }
    }

    private Control RangeRow()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = _metrics.SpacingLarge, RowSpacing = 4 };
        Control Labelled(string label, MeasureField field, ContentControl picture)
        {
            var title = new TextBlock { Text = label, FontSize = _metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
            field.AttachScrubber(title);
            return new StackPanel { Spacing = 4, Children = { title, picture, field } };
        }
        var from = Labelled(Texts["deck.setup.from"], _from, _fromPicture);
        var to = Labelled(Texts["deck.setup.to"], _to, _toPicture);
        Grid.SetColumn(to, 1);
        row.Children.Add(from);
        row.Children.Add(to);
        return row;
    }

    private void Refresh()
    {
        _rangeRow.IsEnabled = _custom;
        _rangeRow.Opacity = _custom ? 1 : 0.5;
        if (_to.Value < _from.Value) _to.Value = _from.Value;
        _fromPicture.Content = Picture((int)Math.Round(_from.Value));
        _toPicture.Content = Picture((int)Math.Round(_to.Value));
    }

    private Control Picture(int number)
    {
        if (_deck.Slides.Count == 0) return new Border { Width = ThumbnailWidth };
        var slide = _deck.Slides[Math.Clamp(number - 1, 0, _deck.Slides.Count - 1)];
        var ratio = _deck.Size.Width.Value > 0 ? (double)_deck.Size.Height.Value / _deck.Size.Width.Value : 9.0 / 16.0;
        return new Border
        {
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _services.Pictures.Create(_deck, slide, ThumbnailWidth, ThumbnailWidth * ratio),
        };
    }

    private MeasureField SlideNumber(int value, int count, string name) =>
        new(new MeasureEntry(new MeasureOptions("#", 1, count, 1) { Integer = true, LargeStep = 5 }, _services.Units, CultureInfo.CurrentCulture),
            value, Texts, _metrics, _services.Icons, name, boxWidth: 48)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
}
