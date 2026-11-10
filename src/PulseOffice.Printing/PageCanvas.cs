using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public readonly record struct PrintColor(byte R, byte G, byte B)
{
    public static readonly PrintColor Black = new(0, 0, 0);
    public static readonly PrintColor White = new(255, 255, 255);
    public static readonly PrintColor Grid = new(190, 196, 206);

    public static PrintColor? FromName(string? name) => name?.ToUpperInvariant() switch
    {
        "BLACK" => Black,
        "WHITE" => White,
        "RED" => new PrintColor(220, 38, 38),
        "BLUE" => new PrintColor(37, 99, 235),
        "GREEN" => new PrintColor(22, 163, 74),
        "CYAN" => new PrintColor(8, 145, 178),
        "MAGENTA" => new PrintColor(192, 38, 211),
        "YELLOW" => new PrintColor(202, 138, 4),
        _ => null,
    };

    public static PrintColor FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        var text = hex.TrimStart('#');
        if (text.Length == 8) text = text[2..];

        if (text.Length != 6 || !uint.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var value)) return Black;

        return new PrintColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
}

public enum PrintUnderline
{
    None,
    Single,
    Double,
}

public sealed record PrintFont(string Family, double SizePoints, bool Bold = false, bool Italic = false)
{
    public static readonly PrintFont Default = new("Helvetica", 10);

    public PrintUnderline Underline { get; init; }

    public bool Strikethrough { get; init; }

    public bool IsDecorated => Underline != PrintUnderline.None || Strikethrough;

    public PrintFont Undecorated => IsDecorated ? this with { Underline = PrintUnderline.None, Strikethrough = false } : this;
}

public enum TextAlignment
{
    Left,
    Center,
    Right,
}

public enum TextBaseline
{
    Top,
    Middle,
    Bottom,
}

public interface IPageCanvas
{
    DocumentSize PageSize { get; }

    Twip MeasureText(string text, PrintFont font);

    void FillRectangle(DocumentRect rect, PrintColor color);

    void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness);

    void DrawText(
        string text, DocumentRect bounds, PrintFont font, PrintColor color,
        TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle);

    void DrawPath(PrintPath path);

    void DrawImage(PageImage image, DocumentRect bounds);

    IDisposable Clip(DocumentRect area) => new PageClipScope(null);

    void MarkHeading(int level, string title, double topPoints)
    {
    }

    void MarkTarget(string name, double topPoints)
    {
    }

    void MarkLink(DocumentRect area, string target)
    {
    }

    void MarkNote(DocumentRect area, PrintNote note)
    {
    }

    void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var left = run.X;
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(run.Text);
        while (elements.MoveNext())
        {
            var glyph = (string)elements.Current;
            var advance = run.Advance(elements.ElementIndex, glyph.Length);
            if (!string.IsNullOrWhiteSpace(glyph) && !char.IsControl(glyph[0]) && glyph != "\u00AD")
            {
                var bounds = new DocumentRect(Twip.FromPoints(left), Twip.FromPoints(run.Baseline - run.Font.SizePoints * 0.8),
                    Twip.FromPoints(Math.Max(advance, run.Font.SizePoints)), Twip.FromPoints(run.Font.SizePoints * 1.2));
                DrawText(glyph, bounds, run.Font.Undecorated, run.Color, TextAlignment.Left, TextBaseline.Top);
            }
            left += advance;
        }
        new TextDecorationPainter().Paint(this, new PrintTextSpan(run.X, run.Baseline, left - run.X, run.Font, run.Color));
    }
}

public sealed class PageClipScope(Action? release) : IDisposable
{
    private Action? _release = release;

    public void Dispose()
    {
        var release = _release;
        _release = null;
        release?.Invoke();
    }
}

public sealed record PrintNote(string Author, string Text, DateTimeOffset? When = null);

public sealed record PrintHeading(int Level, string Title, double TopPoints);

public sealed record PrintDestination(string Name, double TopPoints);

public sealed record PrintLink(DocumentRect Area, string Target);

public sealed record PrintMarkedNote(DocumentRect Area, PrintNote Note);

public sealed record PrintGlyphRun(string Text, double X, double Baseline, PrintFont Font, PrintColor Color, IReadOnlyList<double> Advances)
{
    public double HorizontalScale { get; init; } = 1;

    public double RotationDegrees { get; init; }

    public double Advance(int index, int length)
    {
        var total = 0.0;
        for (var offset = index; offset < index + length && offset < Advances.Count; offset++) total += Advances[offset];
        return total;
    }

    public double Width => Advances.Sum();
}

public sealed record PageImageSource(ReadOnlyMemory<byte> Data, string MediaType);

public sealed record PageImage(int Width, int Height, byte[] Rgb)
{
    public byte[]? Alpha { get; init; }

    public PageImageSource? Source { get; init; }
}

public sealed record PrintGradient(
    PrintColor From, PrintColor To, double AngleDegrees, bool Radial);

public sealed record PrintPath(ShapeOutline Outline, DocumentRect Bounds)
{
    public PrintColor? Fill { get; init; }

    public PrintGradient? Gradient { get; init; }

    public double FillOpacity { get; init; } = 1;

    public PrintColor? Shadow { get; init; }

    public Twip ShadowOffsetX { get; init; } = Twip.Zero;

    public Twip ShadowOffsetY { get; init; } = Twip.Zero;

    public double ShadowOpacity { get; init; } = 0.35;

    public PrintColor? Stroke { get; init; }

    public Twip StrokeWidth { get; init; } = Twip.FromPoints(1);

    public double RotationDegrees { get; init; }

    public bool FlipHorizontal { get; init; }

    public bool FlipVertical { get; init; }
}

public interface IPrintableDocument
{
    PageSetup Setup { get; }

    int PageCount { get; }

    void PaintPage(int index, IPageCanvas canvas);
}
