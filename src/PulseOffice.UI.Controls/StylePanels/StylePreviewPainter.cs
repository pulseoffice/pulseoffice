using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.StylePanels;

public sealed class StylePreviewPainter(DesignTokens.Metrics metrics)
{
    private const double Indent = 14;
    private const double SmallestSize = 11;
    private const double LargestSize = 17;

    public Control Row(StyleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var preview = row.Entry.Preview;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*"),
            MinHeight = metrics.RowHeight + 6,
        };

        var marker = new Border
        {
            Width = 3,
            Margin = new Thickness(0, 4, 0, 4),
            CornerRadius = new CornerRadius(2),
            IsVisible = row.IsCurrent,
        }.Themed(Border.BackgroundProperty, "PulseAccent");
        grid.Children.Add(marker);

        var guides = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(metrics.SpacingSmall, 0, 0, 0) };
        for (var level = 0; level < row.Depth; level++)
        {
            guides.Children.Add(new Border
            {
                Width = Indent,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Margin = new Thickness(4, 0, 0, 0),
            }.Themed(Border.BorderBrushProperty, Palette.Keys.Border));
        }
        Grid.SetColumn(guides, 1);
        grid.Children.Add(guides);

        if (preview?.Swatch is { } swatch && Parse(swatch) is { } fill)
        {
            var chip = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, metrics.SpacingSmall, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(fill),
                BorderThickness = new Thickness(1),
            };
            if (preview.SwatchBorder is { } edge && Parse(edge) is { } line) chip.BorderBrush = new SolidColorBrush(line);
            else chip.Themed(Border.BorderBrushProperty, Palette.Keys.BorderStrong);
            Grid.SetColumn(chip, 2);
            grid.Children.Add(chip);
        }

        var title = new TextBlock
        {
            Text = row.Entry.Title,
            FontSize = Size(preview),
            FontWeight = preview?.Bold == true ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = preview?.Italic == true ? FontStyle.Italic : FontStyle.Normal,
            TextDecorations = Decorations(preview),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (preview?.Font is { Length: > 0 } font) title.FontFamily = new FontFamily(font + ", " + FontFamily.Default.Name);
        if (preview?.Foreground is { } ink && Parse(ink) is { } color && Readable(color, preview.Background is not null)) title.Foreground = new SolidColorBrush(color);
        else title.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        Control body = title;
        if (preview?.Background is { } back && Parse(back) is { } paper)
        {
            body = new Border
            {
                Background = new SolidColorBrush(paper),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = title,
            };
        }
        body.Margin = new Thickness(row.Depth == 0 ? 2 : 4, 2, metrics.SpacingSmall, 2);
        Grid.SetColumn(body, 3);
        grid.Children.Add(body);
        return grid;
    }

    private double Size(StylePreview? preview) =>
        preview?.Size is { } size ? Math.Clamp(size, SmallestSize, LargestSize) : metrics.FontSize + 1;

    private TextDecorationCollection? Decorations(StylePreview? preview)
    {
        if (preview is null) return null;
        var decorations = new TextDecorationCollection();
        if (preview.Underline) decorations.AddRange(TextDecorations.Underline);
        if (preview.Strikethrough) decorations.AddRange(TextDecorations.Strikethrough);
        return decorations.Count > 0 ? decorations : null;
    }

    private Color? Parse(string text) => Color.TryParse(text, out var color) ? color : null;

    private bool Readable(Color color, bool onOwnBackground)
    {
        if (onOwnBackground) return true;
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        return luminance is > 0.12 and < 0.88;
    }
}
