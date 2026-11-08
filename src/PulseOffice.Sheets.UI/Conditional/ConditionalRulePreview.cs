using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Drawing.ConditionalIcons;
using PulseOffice.Sheets.Formatting;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.Conditional;

public sealed class ConditionalPainting(IConditionalIconCatalog icons, IconPainter painter)
{
    private static readonly IBrush Paper = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IPen Grid = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)), 1);
    private static readonly IBrush Ink = new ImmutableSolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));

    public IConditionalIconCatalog Icons => icons;

    public void Rule(DrawingContext context, Rect box, ConditionalRule rule, string sample)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rule);
        switch (rule.Visual)
        {
            case ConditionalVisual.DataBar:
                Bars(context, box, Brush(rule.BarColorHex), rule.Bar.Gradient);
                break;
            case ConditionalVisual.ColorScale:
                Scale(context, box, rule.ScaleColors);
                break;
            case ConditionalVisual.IconSet:
                IconRow(context, box, rule.IconSetName, rule.ReverseIcons);
                break;
            default:
                Styled(context, box, rule.Style.BackgroundHex, rule.Style.Font.ColorHex, rule.Style.Font.Bold, sample);
                break;
        }
    }

    public void Styled(DrawingContext context, Rect box, string? background, string foreground, bool bold, string sample)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.DrawRectangle(background is { Length: > 0 } ? Brush(background) : Paper, Grid, box);
        var text = new FormattedText(sample, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            Math.Clamp(box.Height * 0.5, 9, 13), Brush(foreground));
        using (context.PushClip(box))
            context.DrawText(text, new Point(box.X + (box.Width - text.Width) / 2, box.Y + (box.Height - text.Height) / 2));
    }

    public void Bars(DrawingContext context, Rect box, IBrush fill, bool gradient)
    {
        ArgumentNullException.ThrowIfNull(context);
        var fractions = new[] { 0.35, 0.8, 0.55 };
        var height = box.Height / fractions.Length;
        for (var index = 0; index < fractions.Length; index++)
        {
            var cell = new Rect(box.X, box.Y + index * height, box.Width, height);
            context.DrawRectangle(Paper, Grid, cell);
            var bar = new Rect(cell.X + 2, cell.Y + 2, Math.Max(0, (cell.Width - 4) * fractions[index]), Math.Max(0, cell.Height - 4));
            IBrush brush = fill;
            if (gradient && fill is ISolidColorBrush solid)
                brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(solid.Color, 0), new GradientStop(Color.FromArgb(0x30, solid.Color.R, solid.Color.G, solid.Color.B), 1) },
                };
            context.DrawRectangle(brush, null, bar);
        }
    }

    public void Scale(DrawingContext context, Rect box, IReadOnlyList<string> colours)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(colours);
        if (colours.Count == 0) return;
        const int cells = 5;
        var height = box.Height / cells;
        for (var index = 0; index < cells; index++)
        {
            var position = index / (double)(cells - 1);
            var cell = new Rect(box.X, box.Y + index * height, box.Width, height);
            context.DrawRectangle(new SolidColorBrush(Mix(colours, 1 - position)), Grid, cell);
        }
    }

    public void IconRow(DrawingContext context, Rect box, string set, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.DrawRectangle(Paper, Grid, box);
        var count = Math.Max(1, icons.Count(set));
        var size = Math.Min(box.Height - 4, (box.Width - 4) / count - 2);
        if (size <= 2) return;
        var total = count * (size + 2) - 2;
        var x = box.X + (box.Width - total) / 2;
        for (var index = 0; index < count; index++)
        {
            var icon = reverse ? count - 1 - index : index;
            painter.Paint(context, icons.Icon(set, icon), new Rect(x + index * (size + 2), box.Y + (box.Height - size) / 2, size, size));
        }
    }

    public void Text(DrawingContext context, Point at, string text, double size, bool bold = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, Ink), at);
    }

    public void Column(DrawingContext context, Rect box, IReadOnlyList<(string Text, string? Fill, string? Ink)> cells)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cells);
        var height = box.Height / Math.Max(1, cells.Count);
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = new Rect(box.X, box.Y + index * height, box.Width, height);
            var (text, fill, ink) = cells[index];
            context.DrawRectangle(fill is null ? Paper : Brush(fill), Grid, cell);
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default,
                Math.Clamp(height * 0.55, 7, 12), ink is null ? Ink : Brush(ink));
            using (context.PushClip(cell))
                context.DrawText(formatted, new Point(cell.Right - formatted.Width - 4, cell.Y + (height - formatted.Height) / 2));
        }
    }

    private static IBrush Brush(string hex) =>
        Color.TryParse(hex, out var colour) ? new ImmutableSolidColorBrush(colour) : Ink;

    private static Color Mix(IReadOnlyList<string> colours, double position)
    {
        var parsed = colours.Select(hex => Color.TryParse(hex, out var colour) ? colour : Colors.White).ToList();
        if (parsed.Count == 1) return parsed[0];
        var scaled = Math.Clamp(position, 0, 1) * (parsed.Count - 1);
        var low = (int)Math.Floor(scaled);
        var high = Math.Min(parsed.Count - 1, low + 1);
        var amount = scaled - low;
        byte Lerp(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return Color.FromRgb(Lerp(parsed[low].R, parsed[high].R), Lerp(parsed[low].G, parsed[high].G), Lerp(parsed[low].B, parsed[high].B));
    }
}

public sealed class ConditionalRulePreview(ConditionalPainting painting, string sample) : Control
{
    private ConditionalRule? _rule;

    public ConditionalRule? Rule
    {
        get => _rule;
        set
        {
            _rule = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_rule is null || Bounds.Width <= 2 || Bounds.Height <= 2) return;
        painting.Rule(context, new Rect(Bounds.Size).Deflate(0.5), _rule, sample);
    }
}

public sealed class ConditionalKindThumbnail(ConditionalPainting painting, IConditionalRuleCatalog catalog, ConditionalRuleKind kind) : Control
{
    public override void Render(DrawingContext context)
    {
        var box = new Rect(Bounds.Size).Deflate(new Thickness(Bounds.Width * 0.22, 3));
        if (box.Width <= 2 || box.Height <= 2) return;
        var preset = catalog.StylePresets[0];
        var hit = (preset.BackgroundHex, (string?)preset.FontHex);
        (string, string?, string?) Plain(string text) => (text, null, null);
        (string, string?, string?) Hit(string text) => (text, hit.BackgroundHex, hit.Item2);
        switch (kind)
        {
            case ConditionalRuleKind.DataBar:
                painting.Bars(context, box, new SolidColorBrush(Color.Parse(catalog.BarColors[0])), true);
                break;
            case ConditionalRuleKind.ColorScale:
                painting.Scale(context, box, catalog.ScalePresets[0].Colors);
                break;
            case ConditionalRuleKind.IconSet:
                painting.IconRow(context, box, "3Arrows", false);
                break;
            case ConditionalRuleKind.CellValue:
                painting.Column(context, box, [Plain("12"), Hit("87"), Plain("40"), Hit("95")]);
                break;
            case ConditionalRuleKind.TopBottom:
                painting.Column(context, box, [Hit("98"), Plain("35"), Hit("91"), Plain("12")]);
                break;
            case ConditionalRuleKind.Average:
                painting.Column(context, box, [Plain("20"), Hit("70"), Plain("30"), Hit("80")]);
                break;
            case ConditionalRuleKind.Duplicates:
                painting.Column(context, box, [Hit("Rossi"), Plain("Bianchi"), Hit("Rossi"), Plain("Verdi")]);
                break;
            case ConditionalRuleKind.Text:
                painting.Column(context, box, [Plain("aaa"), Hit("abc"), Plain("xyz"), Hit("abd")]);
                break;
            case ConditionalRuleKind.Date:
                painting.Column(context, box, [Plain("1/3"), Hit("8/3"), Plain("7/3"), Plain("9/3")]);
                break;
            case ConditionalRuleKind.Blanks:
                painting.Column(context, box, [Plain("4"), Hit(string.Empty), Plain("8"), Hit(string.Empty)]);
                break;
            default:
                painting.Column(context, box, [Plain("=A1"), Hit("fx"), Plain("=B2"), Plain("=C3")]);
                break;
        }
    }
}

public sealed class ConditionalKindPainter(ConditionalPainting painting, IConditionalRuleCatalog catalog) : IThumbnailPainter<ConditionalRuleKind>
{
    public Control Create(ConditionalRuleKind item, Size size) => new ConditionalKindThumbnail(painting, catalog, item) { Height = size.Height };

    public void Repaint(Control thumbnail, ConditionalRuleKind item) => thumbnail.InvalidateVisual();
}

public sealed class ColorScalePainter(ConditionalPainting painting) : IThumbnailPainter<ColorScalePreset>
{
    public Control Create(ColorScalePreset item, Size size) => new ScaleThumbnail(painting, item) { Height = size.Height };

    public void Repaint(Control thumbnail, ColorScalePreset item) => thumbnail.InvalidateVisual();

    private sealed class ScaleThumbnail(ConditionalPainting painting, ColorScalePreset preset) : Control
    {
        public override void Render(DrawingContext context) =>
            painting.Scale(context, new Rect(Bounds.Size).Deflate(new Thickness(Bounds.Width * 0.25, 2)), preset.Colors);
    }
}

public sealed class IconSetPainter(ConditionalPainting painting) : IThumbnailPainter<string>
{
    public Control Create(string item, Size size) => new SetThumbnail(painting, item) { Height = size.Height };

    public void Repaint(Control thumbnail, string item) => thumbnail.InvalidateVisual();

    private sealed class SetThumbnail(ConditionalPainting painting, string set) : Control
    {
        public override void Render(DrawingContext context) =>
            painting.IconRow(context, new Rect(Bounds.Size).Deflate(2), set, false);
    }
}
