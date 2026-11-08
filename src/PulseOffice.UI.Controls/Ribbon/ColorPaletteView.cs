using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed record ColorPaletteLabels(string None, string Theme, string Standard, string? More);

public sealed class ColorPaletteView : UserControl
{
    private const double SwatchSize = 18;

    private readonly DesignTokens.Metrics _metrics;

    public event EventHandler<RibbonColor>? Chosen;

    public event EventHandler? MoreRequested;

    private bool _marked;

    public ColorPaletteView(RibbonPalette palette, ColorPaletteLabels labels, string? current, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(labels);

        _metrics = metrics;

        var layout = new StackPanel { Spacing = metrics.SpacingSmall, MinWidth = 10 * (SwatchSize + 3) };

        var none = Wide(labels.None, () => Chosen?.Invoke(this, new RibbonColor(null)), None());
        if (current is null)
        {
            none.Classes.Add("checked");
            _marked = true;
        }

        layout.Children.Add(none);

        if (palette.ThemeColumns.Count > 0)
        {
            layout.Children.Add(Caption(labels.Theme));
            layout.Children.Add(ThemeGrid(palette.ThemeColumns, current));
        }

        if (palette.Standard.Count > 0)
        {
            layout.Children.Add(Caption(labels.Standard));
            layout.Children.Add(Row(palette.Standard, current));
        }

        if (labels.More is { } more)
            layout.Children.Add(Wide(more, () => MoreRequested?.Invoke(this, EventArgs.Empty), null));

        Content = layout;
    }

    private Control ThemeGrid(IReadOnlyList<IReadOnlyList<string>> columns, string? current)
    {
        var grid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };

        foreach (var column in columns)
        {
            var stack = new StackPanel { Spacing = 0 };

            for (var index = 0; index < column.Count; index++)
            {
                var swatch = Swatch(column[index], current);
                swatch.Margin = new Thickness(0, index == 1 ? 4 : 0, 0, 0);
                stack.Children.Add(swatch);
            }

            grid.Children.Add(stack);
        }

        return grid;
    }

    private Control Row(IReadOnlyList<string> colours, string? current)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };

        foreach (var colour in colours) row.Children.Add(Swatch(colour, current));

        return row;
    }

    private Button Swatch(string colour, string? current)
    {
        var selected = !_marked && string.Equals(colour, current, StringComparison.OrdinalIgnoreCase);
        _marked |= selected;

        var button = new Button
        {
            Width = SwatchSize,
            Height = SwatchSize,
            Padding = default,
            BorderThickness = new Thickness(selected ? 2 : 1),
            Background = Color.TryParse(colour, out var parsed) ? new SolidColorBrush(parsed) : Brushes.Transparent,
            CornerRadius = new CornerRadius(2),
            [ToolTip.TipProperty] = colour,
        };

        button.Classes.Add("swatch");
        button.Themed(Button.BorderBrushProperty, selected ? "PulseAccent" : Palette.Keys.Border);
        button.Click += (_, _) => Chosen?.Invoke(this, new RibbonColor(colour));

        return button;
    }

    private Control None()
    {
        var box = new Border
        {
            Width = SwatchSize - 2,
            Height = SwatchSize - 2,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Child = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(1, SwatchSize - 4),
                EndPoint = new Point(SwatchSize - 4, 1),
                StrokeThickness = 1.2,
                Stroke = Brushes.IndianRed,
            },
        };

        return box.Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong);
    }

    private TextBlock Caption(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(),
        FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
        LetterSpacing = 0.6,
        Margin = new Thickness(0, _metrics.SpacingSmall / 2, 0, 0),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private Button Wide(string text, Action click, Control? glyph)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (glyph is not null) content.Children.Add(glyph);

        content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = _metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var button = new Button
        {
            Content = content,
            Height = _metrics.ControlHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(_metrics.SpacingSmall, 0),
        };

        button.Classes.Add("ribbon");
        button.Click += (_, _) => click();

        return button;
    }
}
