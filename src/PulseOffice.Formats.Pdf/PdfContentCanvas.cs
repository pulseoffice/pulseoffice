using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Units;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Printing;
using PulseOffice.Formats.Pdf.Fonts;

namespace PulseOffice.Formats.Pdf;

public sealed class PdfContentCanvas(DocumentSize pageSize, IPdfFontSet fonts, IArcFlattener? flattener = null, ITextDecorationPainter? decorations = null) : IPageCanvas
{
    private readonly ITextDecorationPainter _decorations = decorations ?? new TextDecorationPainter();

    private readonly PathReplay _replay = new();

    private readonly StringBuilder _content = new();
    private readonly HashSet<IPdfFontResource> _fonts = [];
    private readonly Dictionary<string, double> _alphas = new(StringComparer.Ordinal) { ["GSfull"] = 1 };
    private readonly Dictionary<string, string> _patterns = new(StringComparer.Ordinal);
    private readonly List<(string Name, PdfImagePlacement Image)> _images = [];

    public DocumentSize PageSize { get; } = pageSize;

    public IReadOnlyCollection<IPdfFontResource> UsedFonts => _fonts;

    public IReadOnlyDictionary<string, double> UsedAlphas => _alphas;

    public IReadOnlyDictionary<string, string> UsedPatterns => _patterns;

    public IReadOnlyList<(string Name, PdfImagePlacement Image)> UsedImages => _images;

    public string Content => _content.ToString();

    private readonly List<PrintHeading> _headings = [];
    private readonly List<PrintDestination> _destinations = [];
    private readonly List<PrintLink> _links = [];
    private readonly List<PrintMarkedNote> _notes = [];

    public IReadOnlyList<PrintHeading> Headings => _headings;

    public IReadOnlyList<PrintDestination> Destinations => _destinations;

    public IReadOnlyList<PrintLink> Links => _links;

    public IReadOnlyList<PrintMarkedNote> Notes => _notes;

    public void MarkHeading(int level, string title, double topPoints)
    {
        if (!string.IsNullOrWhiteSpace(title)) _headings.Add(new PrintHeading(Math.Max(1, level), title.Trim(), topPoints));
    }

    public void MarkTarget(string name, double topPoints)
    {
        if (!string.IsNullOrEmpty(name)) _destinations.Add(new PrintDestination(name, topPoints));
    }

    public void MarkLink(DocumentRect area, string target)
    {
        if (!area.IsEmpty && !string.IsNullOrWhiteSpace(target)) _links.Add(new PrintLink(area, target));
    }

    public void MarkNote(DocumentRect area, PrintNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        _notes.Add(new PrintMarkedNote(area, note));
    }

    public Twip MeasureText(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        return Twip.FromPoints(fonts.Measure(text ?? string.Empty, font));
    }

    public void FillRectangle(DocumentRect rect, PrintColor color)
    {
        if (rect.IsEmpty) return;

        Color(color, stroke: false);

        _content.Append(CultureInfo.InvariantCulture,
            $"{Points(rect.Left)} {Points(Flip(rect.Bottom))} {Points(rect.Width)} {Points(rect.Height)} re f\n");
    }

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
    {
        Color(color, stroke: true);

        _content.Append(CultureInfo.InvariantCulture, $"{Points(thickness)} w\n");
        _content.Append(CultureInfo.InvariantCulture,
            $"{Points(from.X)} {Points(Flip(from.Y))} m {Points(to.X)} {Points(Flip(to.Y))} l S\n");
    }

    private readonly IArcFlattener _flattener = flattener ?? new ArcFlattener();

    public void DrawPath(PrintPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Bounds.IsEmpty) return;
        if (path.Fill is null && path.Gradient is null && path.Stroke is null) return;

        var width = path.Bounds.Width.ToPoints();
        var height = path.Bounds.Height.ToPoints();

        var outline = _flattener.Flatten(path.Outline).Scaled(width, height);

        if (path.Shadow is { } shadow) DrawShadow(path, shadow, width, height);

        _content.Append("q\n");

        if (path.FillOpacity < 1 || path.Gradient is not null) AppendAlpha(path.FillOpacity);

        var placement = AppendTransform(path, width, height);

        string? pattern = null;

        if (path.Fill is { } fill) Color(fill, stroke: false);
        else if (path.Gradient is { } gradient) pattern = Shading(gradient, width, height, placement);

        if (path.Stroke is { } stroke)
        {
            Color(stroke, stroke: true);
            _content.Append(CultureInfo.InvariantCulture, $"{Points(path.StrokeWidth)} w\n");
        }

        var sink = new PdfPathSink(_content, Pair);
        _replay.Replay(outline, sink);

        if (sink.Painted)
        {
            var filled = path.Fill is not null || pattern is not null;

            _content.Append(filled
                ? path.Stroke is not null ? "B\n" : "f\n"
                : "S\n");
        }

        _content.Append("Q\n");
        _content.Append("/GSfull gs\n");
    }

    public IDisposable Clip(DocumentRect area)
    {
        var width = Math.Max(0, area.Width.Value);
        var height = Math.Max(0, area.Height.Value);
        _content.Append("q\n");
        _content.Append(CultureInfo.InvariantCulture,
            $"{Points(area.Left)} {Points(Flip(area.Top + new Twip(height)))} {Points(new Twip(width))} {Points(new Twip(height))} re W n\n");
        return new PageClipScope(() => _content.Append("Q\n"));
    }

    public void DrawImage(PageImage image, DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (bounds.IsEmpty || image.Width <= 0 || image.Height <= 0) return;
        if (image.Rgb is null || image.Rgb.LongLength < (long)image.Width * image.Height * 3) return;

        var name = $"Img{_images.Count + 1}";
        _images.Add((name, new PdfImagePlacement(image, bounds.Width.ToPoints(), bounds.Height.ToPoints())));

        var left = bounds.Left.ToPoints();
        var bottom = Flip(bounds.Bottom).ToPoints();
        var width = bounds.Width.ToPoints();
        var height = bounds.Height.ToPoints();

        _content.Append("q\n");
        _content.Append(CultureInfo.InvariantCulture,
            $"{Round(width)} 0 0 {Round(height)} {Round(left)} {Round(bottom)} cm\n");
        _content.Append(CultureInfo.InvariantCulture, $"/{name} Do\n");
        _content.Append("Q\n");
    }

    private void DrawShadow(PrintPath path, PrintColor colour, double width, double height)
    {
        var shifted = path with
        {
            Bounds = new DocumentRect(
                path.Bounds.Left + path.ShadowOffsetX,
                path.Bounds.Top + path.ShadowOffsetY,
                path.Bounds.Width,
                path.Bounds.Height),
            Fill = colour,
            Gradient = null,
            Stroke = null,
            Shadow = null,
            FillOpacity = path.ShadowOpacity,
        };

        DrawPath(shifted);
    }

    private void AppendAlpha(double opacity)
    {
        if (!double.IsFinite(opacity)) opacity = 1;
        var value = Math.Clamp(opacity, 0, 1);
        var name = $"GS{(int)Math.Round(value * 100)}";

        _alphas[name] = value;

        _content.Append(CultureInfo.InvariantCulture, $"/{name} gs\n");
    }

    private string Shading(PrintGradient gradient, double width, double height, PdfMatrix placement)
    {
        var name = $"P{_patterns.Count + 1}";

        var radians = gradient.AngleDegrees * Math.PI / 180;

        var centreX = width / 2;
        var centreY = height / 2;

        var reach = Math.Max(width, height) / 2;

        var dx = Math.Cos(radians) * reach;
        var dy = Math.Sin(radians) * reach;

        var coordinates = gradient.Radial
            ? $"[{Round(centreX)} {Round(centreY)} 0 {Round(centreX)} {Round(centreY)} {Round(reach)}]"
            : $"[{Round(centreX - dx)} {Round(centreY - dy)} {Round(centreX + dx)} {Round(centreY + dy)}]";

        _patterns[name] =
            $"<< /PatternType 2 /Matrix [{placement.Format(Round)}] /Shading << /ShadingType {(gradient.Radial ? 3 : 2)} " +
            $"/ColorSpace /DeviceRGB /Coords {coordinates} " +
            $"/Function << /FunctionType 2 /Domain [0 1] /C0 [{Channels(gradient.From)}] " +
            $"/C1 [{Channels(gradient.To)}] /N 1 >> /Extend [true true] >> >>";

        _content.Append(CultureInfo.InvariantCulture, $"/Pattern cs /{name} scn\n");

        return name;
    }

    private static string Channels(PrintColor colour) =>
        $"{Round(colour.R / 255.0)} {Round(colour.G / 255.0)} {Round(colour.B / 255.0)}";

    private PdfMatrix AppendTransform(PrintPath path, double width, double height)
    {
        var left = path.Bounds.Left.ToPoints();
        var top = Flip(path.Bounds.Top).ToPoints();

        var total = PdfMatrix.Identity;
        void Append(double a, double b, double c, double d, double e, double f)
        {
            var step = new PdfMatrix(a, b, c, d, e, f);
            total = step.Then(total);
            AppendMatrix(step);
        }

        Append(1, 0, 0, -1, left, top);

        if (path.FlipHorizontal || path.FlipVertical)
        {
            Append(1, 0, 0, 1, width / 2, height / 2);
            Append(path.FlipHorizontal ? -1 : 1, 0, 0, path.FlipVertical ? -1 : 1, 0, 0);
            Append(1, 0, 0, 1, -width / 2, -height / 2);
        }

        if (Math.Abs(path.RotationDegrees) > 0.001)
        {
            var radians = path.RotationDegrees * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            Append(1, 0, 0, 1, width / 2, height / 2);
            Append(cos, sin, -sin, cos, 0, 0);
            Append(1, 0, 0, 1, -width / 2, -height / 2);
        }

        return total;
    }

    private void AppendMatrix(PdfMatrix matrix) =>
        _content.Append(CultureInfo.InvariantCulture, $"{matrix.Format(Round)} cm\n");

    private static string Pair(ShapePoint point) => $"{Round(point.X)} {Round(point.Y)}";

    private static string Round(double value) =>
        (double.IsFinite(value) ? Math.Round(value, 3) + 0.0 : 0).ToString("0.###", CultureInfo.InvariantCulture);

    public void DrawText(
        string text, DocumentRect bounds, PrintFont font, PrintColor color,
        TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
    {
        if (string.IsNullOrEmpty(text) || bounds.IsEmpty) return;

        ArgumentNullException.ThrowIfNull(font);
        if (!(font.SizePoints > 0) || !double.IsFinite(font.SizePoints)) return;

        var width = Twip.FromPoints(fonts.Measure(text, font));
        var visible = width.Value <= bounds.Width.Value ? text : Elide(text, font, bounds.Width);

        if (visible.Length == 0) return;

        var visibleWidth = Twip.FromPoints(fonts.Measure(visible, font));

        var x = alignment switch
        {
            TextAlignment.Center => bounds.Left.Value + (bounds.Width.Value - visibleWidth.Value) / 2,
            TextAlignment.Right => bounds.Right.Value - visibleWidth.Value,
            _ => bounds.Left.Value,
        };

        var size = Twip.FromPoints(font.SizePoints).Value;

        var y = baseline switch
        {
            TextBaseline.Top => bounds.Top.Value + (int)(size * 0.8),
            TextBaseline.Bottom => bounds.Bottom.Value - (int)(size * 0.2),
            _ => bounds.Top.Value + (bounds.Height.Value + (int)(size * 0.7)) / 2,
        };

        var composer = new PdfTextComposer();
        foreach (var part in fonts.Show(visible, font)) composer.Add(part);
        if (composer.FirstFont is not { } first) return;

        Color(color, stroke: false);
        var synthetic = Synthetic(first, font, color);

        _content.Append(CultureInfo.InvariantCulture,
            $"BT 1 0 {Number(synthetic ? SlantFor(first, font) : 0)} 1 {Points(new Twip((int)x))} {Points(Flip(new Twip(y)))} Tm ");
        _content.Append(composer.Compose(font.SizePoints, resource => Select(resource, font)));
        _content.Append("ET\n");
        if (synthetic) _content.Append("0 Tr\n");
        _decorations.Paint(this, new PrintTextSpan(new Twip((int)x).ToPoints(), new Twip(y).ToPoints(), visibleWidth.ToPoints(), font, color));
    }

    public void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Text.Length == 0 || !(run.Font.SizePoints > 0) || !double.IsFinite(run.Font.SizePoints)) return;
        if (!double.IsFinite(run.X) || !double.IsFinite(run.Baseline)) return;
        var size = run.Font.SizePoints;
        var scale = run.HorizontalScale > 0 && double.IsFinite(run.HorizontalScale) ? run.HorizontalScale : 1;
        var composer = new PdfTextComposer();
        var spans = fonts.ComplexSpans(run.Text);
        var next = 0;
        var index = 0;
        while (index < run.Text.Length)
        {
            while (next < spans.Count && spans[next].End <= index) next++;
            IReadOnlyList<PdfShownText> shown;
            int length;
            if (next < spans.Count && spans[next].Start == index)
            {
                length = spans[next].Length;
                shown = fonts.Show(run.Text, spans[next], run.Font);
            }
            else
            {
                length = Math.Max(1, StringInfo.GetNextTextElementLength(run.Text, index));
                if (next < spans.Count && spans[next].Start > index) length = Math.Min(length, spans[next].Start - index);
                var element = run.Text.Substring(index, length);
                shown = char.IsControl(element[0]) || element == "\u00AD" ? [] : fonts.Show(element, run.Font);
            }
            var standard = 0.0;
            foreach (var part in shown)
            {
                composer.Add(part);
                standard += part.Thousandths * size / 1000.0;
            }
            var advance = run.Advance(index, length);
            if (double.IsFinite(advance)) composer.Adjust((standard - advance / scale) * 1000 / size);
            index += length;
        }
        if (composer.FirstFont is not { } first) return;
        Color(run.Color, stroke: false);
        var synthetic = Synthetic(first, run.Font, run.Color);
        var radians = double.IsFinite(run.RotationDegrees) ? run.RotationDegrees * Math.PI / 180 : 0;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var slant = synthetic ? SlantFor(first, run.Font) : 0;
        _content.Append(CultureInfo.InvariantCulture,
            $"BT {Number(scale * cos)} {Number(scale * sin)} {Number(-sin + slant * cos)} {Number(cos + slant * sin)} {Points(Twip.FromPoints(run.X))} {Points(Flip(Twip.FromPoints(run.Baseline)))} Tm ");
        _content.Append(composer.Compose(size, resource => Select(resource, run.Font)));
        _content.Append("ET\n");
        if (synthetic) _content.Append("0 Tr\n");
        _decorations.Paint(this, new PrintTextSpan(run.X, run.Baseline, run.Width, run.Font, run.Color, run.RotationDegrees));
    }

    private string Select(IPdfFontResource resource, PrintFont font)
    {
        _fonts.Add(resource);
        return string.Create(CultureInfo.InvariantCulture, $"/{resource.Name} {Number(font.SizePoints)} Tf ");
    }

    private bool Synthetic(IPdfFontResource resource, PrintFont font, PrintColor color)
    {
        var bold = font.Bold && !resource.Bold && resource.Embedded;
        var italic = font.Italic && !resource.Italic && resource.Embedded;
        if (!bold && !italic) return false;
        if (bold)
        {
            Color(color, stroke: true);
            _content.Append(CultureInfo.InvariantCulture, $"{Number(font.SizePoints * 0.03)} w 2 Tr\n");
        }
        return true;
    }

    private double SlantFor(IPdfFontResource resource, PrintFont font) =>
        font.Italic && !resource.Italic && resource.Embedded ? 0.2 : 0;

    private string Number(double value) => (double.IsFinite(value) ? value : 0).ToString("0.####", CultureInfo.InvariantCulture);

    private string Elide(string text, PrintFont font, Twip available)
    {
        var ellipsis = fonts.Measure("…", font);
        var budget = available.ToPoints() - ellipsis;

        if (budget <= 0) return string.Empty;

        var kept = 0;
        var used = 0.0;

        while (kept < text.Length)
        {
            var length = char.IsSurrogatePair(text, kept) ? 2 : 1;
            var next = fonts.Measure(text.Substring(kept, length), font);
            if (used + next > budget) break;

            used += next;
            kept += length;
        }

        return kept == 0 ? string.Empty : string.Concat(text.AsSpan(0, kept), "…");
    }

    private Twip Flip(Twip y) => new(PageSize.Height.Value - y.Value);

    private void Color(PrintColor color, bool stroke)
    {
        var operation = stroke ? "RG" : "rg";

        _content.Append(CultureInfo.InvariantCulture,
            $"{Channel(color.R)} {Channel(color.G)} {Channel(color.B)} {operation}\n");
    }

    private static string Channel(byte value) =>
        (value / 255.0).ToString("0.###", CultureInfo.InvariantCulture);

    private static string Points(Twip value) =>
        value.ToPoints().ToString("0.###", CultureInfo.InvariantCulture);

    private sealed class PdfPathSink(System.Text.StringBuilder content, Func<ShapePoint, string> pair) : PathSink
    {
        private bool _open;

        public bool Painted { get; private set; }

        public override void BeginFigure() => _open = false;

        public override void MoveTo(ShapePoint to)
        {
            content.Append(CultureInfo.InvariantCulture, $"{pair(to)} m\n");
            _open = Painted = true;
        }

        public override void LineTo(ShapePoint to)
        {
            if (_open) content.Append(CultureInfo.InvariantCulture, $"{pair(to)} l\n");
        }

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to)
        {
            if (_open) content.Append(CultureInfo.InvariantCulture, $"{pair(first)} {pair(second)} {pair(to)} c\n");
        }

        public override void ArcTo(ShapePoint from, ArcSegment arc)
        {
        }

        public override void Close()
        {
            if (_open) content.Append("h\n");
        }
    }
}
