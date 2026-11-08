using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class ColorSplitSlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is ColorSplitSpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new ColorSplitSlot(item, form, context);
}

public sealed class ColorSplitSlot : RibbonSlot
{
    private readonly Border _bar = new() { Height = 3, CornerRadius = new CornerRadius(1) };
    private Button? _main;
    private Button? _arrow;

    public ColorSplitSlot(RibbonItem item, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context) => Initialise();

    private ColorSplitSpec Spec => (ColorSplitSpec)Item.Control!;

    protected override Control Build()
    {
        var face = new StackPanel
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Context.Icon(Item.Command.Id.Value) is { } icon)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            face.Children.Add(icon);
        }

        _bar.Width = Context.Metrics.IconSize + 2;
        _bar.Themed(Border.BorderBrushProperty, PulseOffice.UI.Theming.Palette.Keys.BorderStrong);
        face.Children.Add(_bar);

        _main = Context.Flat(face, width: Context.ItemHeight + 2);
        _main.Padding = default;
        _main.CornerRadius = new CornerRadius(Context.Metrics.RadiusTight, 0, 0, Context.Metrics.RadiusTight);
        _main.Click += (_, _) => Context.Invoke(Item.Command, new RibbonColor(Spec.Current()));

        _arrow = Context.Flat(Context.Chevron(0.6), width: 14);
        _arrow.Padding = default;
        _arrow.CornerRadius = new CornerRadius(0, Context.Metrics.RadiusTight, Context.Metrics.RadiusTight, 0);

        Flyout? flyout = null;
        flyout = Context.Popover(Palette);
        _arrow.Click += (_, _) => Context.Dismissal.Toggle(flyout!, _main);

        ToolTip.SetTip(_arrow, Item.Title);

        Control Palette()
        {
            var view = new ColorPaletteView(
                Spec.Palette(),
                Context.PaletteLabels(Spec.NoneKey, Spec.MoreCommandId is not null),
                (Spec.Selected ?? Spec.Current)(),
                Context.Metrics);

            view.Chosen += (_, colour) =>
            {
                flyout?.Hide();
                Context.Invoke(Item.Command, colour);
            };

            view.MoreRequested += (_, _) =>
            {
                flyout?.Hide();
                if (Spec.MoreCommandId is { } more) Context.Invoke(more);
            };

            var layout = new StackPanel { Spacing = 2 };
            layout.Children.Add(Context.Caption(Item.Title));
            layout.Children.Add(view);
            return layout;
        }

        return new StackPanel { Orientation = Orientation.Horizontal, Children = { _main, _arrow } };
    }

    protected override void Update()
    {
        var current = Spec.Current();

        _bar.Background = current is not null && Color.TryParse(current, out var colour)
            ? new SolidColorBrush(colour)
            : Brushes.Transparent;

        _bar.BorderThickness = current is null ? new Thickness(1) : default;

        if (_main is not null) ToolTip.SetTip(_main, Context.Tooltip(Item));
    }
}
