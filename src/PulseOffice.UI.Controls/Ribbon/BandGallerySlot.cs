using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class BandGallerySlotFactory(IRibbonBandLayout layout, IColorContrast contrast) : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is ChoiceSpec { FillsHeight: true };

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) => form switch
    {
        RibbonForm.Compact => new ChoiceSlot(item, form, context, contrast),
        RibbonForm.Popover => new BandGridSlot(item, form, context),
        _ => new BandGallerySlot(item, form, context, layout),
    };
}

public sealed class BandGridSlot : RibbonSlot
{
    private readonly ContentControl _host = new();

    public BandGridSlot(RibbonItem item, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context) => Initialise();

    protected override Control Build() => _host;

    protected override void Update()
    {
        var spec = (ChoiceSpec)Item.Control!;
        _host.Content = new ChoiceGrid(Context).Build(spec, Item.Title,
            choice => Context.Invoke(Item.Command, choice.Key),
            spec.MoreCommandId is { } more ? () => Context.Invoke(more) : null);
    }
}

public sealed class BandGallerySlot : RibbonSlot
{
    private const double ArrowWidth = 18;

    private readonly IRibbonBandLayout _layout;
    private RibbonBandPanel? _panel;
    private Button? _up;
    private Button? _down;
    private Flyout? _flyout;
    private string? _shownCurrent;
    private IReadOnlyList<RibbonChoice> _shownChoices = [];
    private bool _revealed;

    public BandGallerySlot(RibbonItem item, RibbonForm form, RibbonBuildContext context, IRibbonBandLayout layout)
        : base(item, form, context)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Initialise();
    }

    private ChoiceSpec Spec => (ChoiceSpec)Item.Control!;

    protected override Control Build()
    {
        _flyout = Context.Popover(() => new ChoiceGrid(Context).Build(Spec, Item.Title, Choose,
            Spec.MoreCommandId is { } more ? () =>
            {
                _flyout?.Hide();
                Context.Invoke(more);
            } : null));

        _panel = new RibbonBandPanel(_layout, Spec.Band, Context.Metrics.SpacingSmall / 2 + 1)
        {
            Margin = new Thickness(3),
            Height = Context.ItemHeight * 3 - 8,
        };

        _up = Arrow("ui.chevronUp", () => Scroll(-1));
        _down = Arrow("ui.chevronDown", () => Scroll(1));
        var more = Arrow("ui.galleryMore", () => Context.Dismissal.Toggle(_flyout!, this));
        ToolTip.SetTip(more, Item.Title);

        var arrows = new Grid { RowDefinitions = new RowDefinitions("*,*,*"), Width = ArrowWidth };
        Grid.SetRow(_down, 1);
        Grid.SetRow(more, 2);
        arrows.Children.Add(_up);
        arrows.Children.Add(_down);
        arrows.Children.Add(more);

        var divider = new Border { Width = 1 }.Themed(Border.BackgroundProperty, Palette.Keys.Border);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto") };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(arrows, 2);
        layout.Children.Add(_panel);
        layout.Children.Add(divider);
        layout.Children.Add(arrows);

        var box = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Context.Metrics.Radius),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 1),
            Child = layout,
        };

        box.Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        box.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        box.PointerWheelChanged += (_, e) =>
        {
            Scroll(e.Delta.Y > 0 ? -1 : 1);
            e.Handled = true;
        };

        return box;
    }

    private Button Arrow(string icon, Action run)
    {
        var button = Context.Flat(Context.Icon(icon, Context.Metrics.IconSize * 0.6), width: ArrowWidth, height: double.NaN);
        button.Padding = default;
        button.CornerRadius = default;
        button.VerticalAlignment = VerticalAlignment.Stretch;
        button.Click += (_, _) => run();
        return button;
    }

    private void Scroll(int delta)
    {
        if (_panel is null) return;
        _panel.FirstRow = _layout.Clamp(_panel.FirstRow + delta, _panel.Metrics(0));
        UpdateArrows();
    }

    private void UpdateArrows()
    {
        if (_panel is null || _up is null || _down is null) return;
        var metrics = _panel.Metrics(0);
        _up.IsEnabled = _panel.FirstRow > 0;
        _down.IsEnabled = _panel.FirstRow < metrics.LastFirstRow;
    }

    protected override void Update()
    {
        if (_panel is null) return;

        var choices = Spec.Choices();
        var current = Spec.Current();

        if (choices.SequenceEqual(_shownChoices))
        {
            for (var index = 0; index < choices.Count && index < _panel.Children.Count; index++)
                _panel.Children[index].Classes.Set("checked", choices[index].Key == current);
        }
        else
        {
            _panel.Children.Clear();
            foreach (var choice in choices) _panel.Children.Add(Tile(choice, choice.Key == current));
            _shownChoices = choices;
        }

        var metrics = _panel.Metrics(0);
        _panel.FirstRow = !_revealed || current != _shownCurrent
            ? _layout.Reveal(IndexOf(choices, current), _panel.FirstRow, metrics)
            : _layout.Clamp(_panel.FirstRow, metrics);

        _revealed = true;
        _shownCurrent = current;
        UpdateArrows();
        ToolTip.SetTip(this, Context.Tooltip(Item));
    }

    private int IndexOf(IReadOnlyList<RibbonChoice> choices, string? current)
    {
        for (var index = 0; index < choices.Count; index++)
            if (choices[index].Key == current) return index;
        return -1;
    }

    private Button Tile(RibbonChoice choice, bool selected)
    {
        var metrics = _panel!.Metrics(_panel.Height);
        Control body = Context.Thumbnail(choice.Thumbnail, Math.Max(1, metrics.TileWidth - 6), Math.Max(1, metrics.TileHeight - 6)) is { } thumbnail
            ? thumbnail
            : new TextBlock
            {
                Text = choice.Title,
                FontSize = Context.Metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

        body.HorizontalAlignment = HorizontalAlignment.Stretch;
        body.VerticalAlignment = VerticalAlignment.Stretch;

        var button = Context.Flat(body, width: metrics.TileWidth, height: metrics.TileHeight);
        button.Classes.Add("tile");
        button.Padding = new Thickness(2);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Stretch;
        button.Classes.Set("checked", selected);
        button.Click += (_, _) => Choose(choice);
        ToolTip.SetTip(button, choice.Title);

        return button;
    }

    private void Choose(RibbonChoice choice)
    {
        _flyout?.Hide();
        Context.Invoke(Item.Command, choice.Key);
    }
}
