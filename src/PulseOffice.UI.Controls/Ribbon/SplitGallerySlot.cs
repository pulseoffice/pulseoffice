using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class SplitGallerySlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is SplitGallerySpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new SplitGallerySlot(item, shape, form, context);
}

public sealed class SplitGallerySlot : RibbonSlot
{
    private const double LargeTextWidth = 92;

    private readonly RibbonShape _shape;
    private Button? _main;
    private Button? _arrow;
    private Flyout? _flyout;

    public SplitGallerySlot(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context)
    {
        _shape = shape;
        Initialise();
    }

    private SplitGallerySpec Spec => (SplitGallerySpec)Item.Control!;

    public Button? MainButton => _main;

    public Button? ArrowButton => _arrow;

    public Control Gallery() => new ChoiceGrid(Context).Build(Spec.AsChoices(), Item.Title, Choose,
        Spec.MoreCommandId is { } more ? () =>
        {
            _flyout?.Hide();
            Context.Invoke(more);
        } : null);

    protected override Control Build()
    {
        _flyout = Context.Popover(Gallery);
        return _shape == RibbonShape.Large ? Large() : Inline();
    }

    private Control Large()
    {
        var face = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (Context.Icon(Item.Command.Id.Value, Context.Metrics.IconSize * 1.5) is { } icon)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            face.Children.Add(icon);
        }

        _main = Context.Flat(face, height: double.NaN);
        _main.MinWidth = 64;
        _main.VerticalAlignment = VerticalAlignment.Stretch;
        _main.CornerRadius = new CornerRadius(Context.Metrics.RadiusTight, Context.Metrics.RadiusTight, 0, 0);
        _main.Click += (_, _) => Context.Invoke(Item.Command);

        var label = new StackPanel
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = Item.Title,
                    FontSize = Context.Metrics.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = LargeTextWidth,
                },
            },
        };
        if (Context.Chevron(0.6) is { } chevron)
        {
            chevron.HorizontalAlignment = HorizontalAlignment.Center;
            label.Children.Add(chevron);
        }

        _arrow = Context.Flat(label, height: double.NaN);
        _arrow.Padding = new Thickness(Context.Metrics.SpacingSmall, 2);
        _arrow.CornerRadius = new CornerRadius(0, 0, Context.Metrics.RadiusTight, Context.Metrics.RadiusTight);
        _arrow.Click += (_, _) => Context.Dismissal.Toggle(_flyout!, this);

        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetRow(_arrow, 1);
        layout.Children.Add(_main);
        layout.Children.Add(_arrow);
        return layout;
    }

    private Control Inline()
    {
        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Context.Metrics.SpacingSmall, VerticalAlignment = VerticalAlignment.Center };
        if (Context.Icon(Item.Command.Id.Value) is { } icon) face.Children.Add(icon);
        if (_shape != RibbonShape.Icon)
            face.Children.Add(new TextBlock { Text = Item.Title, FontSize = Context.Metrics.FontSize, VerticalAlignment = VerticalAlignment.Center });

        _main = Context.Flat(face);
        _main.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        _main.CornerRadius = new CornerRadius(Context.Metrics.RadiusTight, 0, 0, Context.Metrics.RadiusTight);
        _main.Click += (_, _) => Context.Invoke(Item.Command);

        _arrow = Context.Flat(Context.Chevron(0.6), width: 14);
        _arrow.Padding = default;
        _arrow.CornerRadius = new CornerRadius(0, Context.Metrics.RadiusTight, Context.Metrics.RadiusTight, 0);
        _arrow.Click += (_, _) => Context.Dismissal.Toggle(_flyout!, _main);

        return new StackPanel { Orientation = Orientation.Horizontal, Children = { _main, _arrow } };
    }

    protected override void Update()
    {
        if (_main is not null) ToolTip.SetTip(_main, Context.Tooltip(Item));
        if (_arrow is not null) ToolTip.SetTip(_arrow, Item.Title);
    }

    private void Choose(RibbonChoice choice)
    {
        _flyout?.Hide();
        Context.Invoke(Item.Command, choice.Key);
    }
}
