using System.Globalization;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public sealed record PrintTextBlockRequest(
    string Text, DocumentRect Bounds, PrintFont Font, PrintColor Color,
    TextAlignment Alignment = TextAlignment.Left, TextBaseline Baseline = TextBaseline.Middle, bool Wrap = true);

public interface IPrintTextBlock
{
    double LineHeightPoints(PrintFont font);

    IReadOnlyList<string> Lines(IPageCanvas canvas, PrintTextBlockRequest request);

    void Draw(IPageCanvas canvas, PrintTextBlockRequest request);
}

public sealed class PrintTextBlock(ITextWrapper wrapper) : IPrintTextBlock
{
    private const double LineSpacing = 1.2;

    public PrintTextBlock()
        : this(new TextWrapper())
    {
    }

    public double LineHeightPoints(PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        return font.SizePoints * LineSpacing;
    }

    public IReadOnlyList<string> Lines(IPageCanvas canvas, PrintTextBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.Text)) return [];

        var text = request.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!request.Wrap) return text.Split('\n', '\r');

        var width = new Twip(Math.Max(1, request.Bounds.Width.Value));
        return wrapper.Wrap(text, width, line => canvas.MeasureText(line, request.Font));
    }

    public void Draw(IPageCanvas canvas, PrintTextBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(request);
        if (!(request.Font.SizePoints > 0) || !double.IsFinite(request.Font.SizePoints)) return;

        var lines = Lines(canvas, request);
        if (lines.Count == 0) return;

        var height = Twip.FromPoints(LineHeightPoints(request.Font));
        var total = height.Value * lines.Count;
        var bounds = request.Bounds;
        var top = request.Baseline switch
        {
            TextBaseline.Top => bounds.Top.Value,
            TextBaseline.Bottom => bounds.Bottom.Value - total,
            _ => bounds.Top.Value + (bounds.Height.Value - total) / 2,
        };

        using var clip = total > bounds.Height.Value ? canvas.Clip(bounds) : null;
        for (var index = 0; index < lines.Count; index++)
        {
            var lineTop = top + height.Value * index;
            if (lineTop + height.Value <= bounds.Top.Value || lineTop >= bounds.Bottom.Value) continue;
            if (lines[index].Length == 0) continue;
            canvas.DrawText(lines[index], new DocumentRect(bounds.Left, new Twip(lineTop), bounds.Width, height),
                request.Font, request.Color, request.Alignment, TextBaseline.Middle);
        }
    }
}

public sealed record PrintRotatedTextRequest(
    string Text, double CentreX, double CentreY, PrintFont Font, PrintColor Color, double RotationDegrees);

public interface IPrintRotatedText
{
    (double Width, double Height) Extent(IPageCanvas canvas, string text, PrintFont font, double rotationDegrees);

    void Draw(IPageCanvas canvas, PrintRotatedTextRequest request);
}

public sealed class PrintRotatedText(IPrintTextBlock block) : IPrintRotatedText
{
    private const double Ascent = 0.8;

    public PrintRotatedText()
        : this(new PrintTextBlock())
    {
    }

    public (double Width, double Height) Extent(IPageCanvas canvas, string text, PrintFont font, double rotationDegrees)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(font);
        var width = canvas.MeasureText(text ?? string.Empty, font).ToPoints();
        var height = block.LineHeightPoints(font);
        var radians = rotationDegrees * Math.PI / 180;
        var (cos, sin) = (Math.Abs(Math.Cos(radians)), Math.Abs(Math.Sin(radians)));
        return (width * cos + height * sin, width * sin + height * cos);
    }

    public void Draw(IPageCanvas canvas, PrintRotatedTextRequest request)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.Text) || !(request.Font.SizePoints > 0)) return;
        if (!double.IsFinite(request.CentreX) || !double.IsFinite(request.CentreY) || !double.IsFinite(request.RotationDegrees)) return;

        var advances = new double[request.Text.Length];
        var elements = StringInfo.GetTextElementEnumerator(request.Text);
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            advances[elements.ElementIndex] = canvas.MeasureText(element, request.Font).ToPoints();
        }

        var width = advances.Sum();
        var height = block.LineHeightPoints(request.Font);
        var (dx, dy) = (-width / 2, -height / 2 + request.Font.SizePoints * Ascent);
        var radians = request.RotationDegrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));

        canvas.DrawGlyphs(new PrintGlyphRun(
            request.Text,
            request.CentreX + dx * cos + dy * sin,
            request.CentreY - dx * sin + dy * cos,
            request.Font, request.Color, advances)
        {
            RotationDegrees = request.RotationDegrees,
        });
    }
}
