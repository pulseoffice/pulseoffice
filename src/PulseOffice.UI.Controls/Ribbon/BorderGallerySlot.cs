using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class BorderGallerySlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is BorderGallerySpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new BorderGallerySlot(item, form, context);
}

public sealed class BorderGallerySlot : RibbonSlot
{
    private const int GridColumns = 8;

    private readonly ContentControl _glyph = new();
    private string _line = "thin";
    private string _colour = "#000000";
    private Flyout? _flyout;

    public BorderGallerySlot(RibbonItem item, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context)
    {
        _line = Spec.Pen().Line;
        _colour = Spec.Pen().ColorHex;
        Initialise();
    }

    private BorderGallerySpec Spec => (BorderGallerySpec)Item.Control!;

    protected override Control Build()
    {
        if (Form == RibbonForm.Popover) return Panel(inline: true);

        var main = Context.Flat(_glyph, width: Context.ItemHeight + 2);
        main.Padding = default;
        main.CornerRadius = new CornerRadius(Context.Metrics.RadiusTight, 0, 0, Context.Metrics.RadiusTight);
        main.Click += (_, _) => Apply(Spec.Last());

        var arrow = Context.Flat(Context.Chevron(0.6), width: 14);
        arrow.Padding = default;
        arrow.CornerRadius = new CornerRadius(0, Context.Metrics.RadiusTight, Context.Metrics.RadiusTight, 0);

        _flyout = Context.Popover(() => Panel(inline: false));
        arrow.Click += (_, _) => Context.Dismissal.Toggle(_flyout!, main);
        ToolTip.SetTip(arrow, Item.Title);

        return new StackPanel { Orientation = Orientation.Horizontal, Children = { main, arrow } };
    }

    protected override void Update()
    {
        var last = Spec.Presets.FirstOrDefault(choice => choice.Key == Spec.Last()) ?? Spec.Presets.FirstOrDefault();

        _glyph.Content = Context.Icon(last?.IconKey ?? Item.Command.Id.Value);
        ToolTip.SetTip(this, last is null ? Context.Tooltip(Item) : $"{Item.Title}: {last.Title}");
    }

    private Control Panel(bool inline)
    {
        var layout = new StackPanel { Spacing = Context.Metrics.SpacingSmall };

        if (!inline) layout.Children.Add(Context.Caption(Item.Title));

        var grid = new Grid();
        var rows = (int)Math.Ceiling(Spec.Presets.Count / (double)GridColumns);

        for (var column = 0; column < GridColumns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        for (var row = 0; row < rows; row++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < Spec.Presets.Count; index++)
        {
            var cell = Preset(Spec.Presets[index]);
            Grid.SetColumn(cell, index % GridColumns);
            Grid.SetRow(cell, index / GridColumns);
            grid.Children.Add(cell);
        }

        layout.Children.Add(grid);
        layout.Children.Add(Context.Caption(Context.Texts["ribbon.border.line"]));
        layout.Children.Add(Lines());
        layout.Children.Add(Context.Caption(Context.Texts["ribbon.border.colour"]));

        var palette = new ColorPaletteView(
            Spec.Palette(), Context.PaletteLabels("ribbon.colour.automatic", more: false), _colour, Context.Metrics);

        palette.Chosen += (_, colour) =>
        {
            _colour = colour.Hex ?? "#000000";
            Rebuild();
        };

        layout.Children.Add(palette);

        if (Spec.MoreCommandId is { } more)
        {
            var button = Context.Flat(new TextBlock
            {
                Text = Context.Texts["ribbon.border.more"],
                FontSize = Context.Metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
            });

            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
            button.Click += (_, _) =>
            {
                _flyout?.Hide();
                Context.Invoke(more);
            };

            layout.Children.Add(button);
        }

        return layout;
    }

    private void Rebuild()
    {
        if (Form == RibbonForm.Popover) Content = Panel(inline: true);
        else if (_flyout?.Content is Border surface && surface.Child is ScrollViewer viewer) viewer.Content = Panel(inline: false);
    }

    private Control Preset(RibbonChoice choice)
    {
        Control content = (Control?)Context.Icon(choice.IconKey, Context.Metrics.IconSize * 1.2) ?? new TextBlock { Text = choice.Title };

        var button = Context.Flat(content, width: Context.ItemHeight + 8, height: Context.ItemHeight + 8);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Padding = default;
        button.Classes.Set("checked", choice.Key == Spec.Last());
        button.Click += (_, _) => Apply(choice.Key);
        ToolTip.SetTip(button, choice.Title);

        return button;
    }

    private Control Lines()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };

        foreach (var line in Spec.Lines)
        {
            var button = Context.Flat(Sample(line.Key), width: 40);
            button.Classes.Set("checked", line.Key == _line);
            button.Click += (_, _) =>
            {
                _line = line.Key;
                Rebuild();
            };

            ToolTip.SetTip(button, line.Title);
            row.Children.Add(button);
        }

        return row;
    }

    private Control Sample(string key)
    {
        var canvas = new Canvas { Width = 28, Height = 12 };

        var thickness = key switch
        {
            "medium" => 2,
            "thick" => 3,
            _ => 1,
        };

        Avalonia.Collections.AvaloniaList<double>? dashes = key switch
        {
            "dashed" => [4, 2],
            "dotted" => [1, 2],
            _ => null,
        };

        foreach (var y in key == "double" ? new[] { 4.0, 8.0 } : new[] { 6.0 })
        {
            var line = new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(28, y),
                StrokeThickness = thickness,
                StrokeDashArray = dashes,
            };

            line.Themed(Shape.StrokeProperty, PulseOffice.UI.Theming.Palette.Keys.TextPrimary);
            canvas.Children.Add(line);
        }

        return canvas;
    }

    private void Apply(string preset)
    {
        _flyout?.Hide();
        Context.Invoke(Item.Command, new RibbonBorder(preset, _line, _colour));
    }
}
