using PulseOffice.Documents.Layout.Tree;
using LayoutGlyphRun = PulseOffice.Documents.Layout.Tree.GlyphRun;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Rendering;

public sealed record PageMarks(bool FieldShadings)
{
    public bool FormattingMarks { get; init; }

    public bool Boundaries { get; init; }

    public bool Images { get; init; } = true;

    public Func<LineBox, uint?>? ChangeBar { get; init; }

    public Func<LineBox, int?>? LineNumber { get; init; }

    public PulseOffice.Documents.Styles.LineNumbering? Numbering { get; init; }

    public Func<string, uint?>? ParagraphSpotlight { get; init; }

    public Func<PulseOffice.Text.Rich.TextRun, string?>? FieldName { get; init; }

    public Func<string, int, uint?>? RunSpotlight { get; init; }

    public static readonly PageMarks None = new(false);
}

public interface IFolioViewMarks
{
    bool FieldShadings { get; set; }

    bool Comments { get; set; }

    bool ResolvedComments { get; set; }

    bool PrintComments { get; set; }

    bool FormattingMarks { get; set; }

    bool Boundaries { get; set; }

    bool Images { get; set; }

    PageMarks Current { get; }

    event EventHandler? Changed;
}

public sealed class FolioViewMarks : IFolioViewMarks
{
    public bool FieldShadings
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public bool Comments
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public bool ResolvedComments
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public bool PrintComments { get; set; }

    public bool FormattingMarks
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Boundaries
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public bool Images
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public PageMarks Current => new(FieldShadings) { FormattingMarks = FormattingMarks, Boundaries = Boundaries, Images = Images };

    public event EventHandler? Changed;
}

public interface IFolioLineNumberMarks
{
    PageMarks Apply(PageMarks marks, TextDocument document, DocumentLayout layout);
}

public sealed class FolioLineNumberMarks(PulseOffice.Documents.Layout.Queries.ILineNumbers numbers) : IFolioLineNumberMarks
{
    private (DocumentLayout Layout, PulseOffice.Documents.Styles.LineNumbering Numbering, IReadOnlyDictionary<LineBox, int> Table)? _last;

    public PageMarks Apply(PageMarks marks, TextDocument document, DocumentLayout layout)
    {
        if (document.LineNumbering is not { } numbering) return marks;
        var table = Table(layout, numbering);
        return marks with { Numbering = numbering, LineNumber = line => table.TryGetValue(line, out var number) ? number : null };
    }

    private IReadOnlyDictionary<LineBox, int> Table(DocumentLayout layout, PulseOffice.Documents.Styles.LineNumbering numbering)
    {
        if (_last is { } last && ReferenceEquals(last.Layout, layout) && last.Numbering == numbering) return last.Table;
        var table = numbers.Number(layout, numbering);
        _last = (layout, numbering, table);
        return table;
    }
}

public interface IPageRenderer
{
    void Paint(IPageSurface surface, PageBox page, Func<string, PulseOffice.Document.Embedding.EmbeddedObject?>? contents = null, PageMarks? marks = null);
}

public sealed class PageRenderer(IBulletPictureObjects bulletPictures) : IPageRenderer
{
    public void Paint(IPageSurface surface, PageBox page, Func<string, PulseOffice.Document.Embedding.EmbeddedObject?>? contents = null, PageMarks? marks = null) =>
        page.Accept(new PagePaint(surface, contents, marks ?? PageMarks.None, bulletPictures));

    private sealed class PagePaint(IPageSurface surface, Func<string, PulseOffice.Document.Embedding.EmbeddedObject?>? contents, PageMarks marks, IBulletPictureObjects bulletPictures) : ILayoutVisitor
    {
        private const uint Ink = 0xFF000000;

        private const uint FieldShade = 0xFFD8D8D8;

        private const uint Light = 0xFFFFFFFF;

        private const uint Dark = 0xFF808080;

        private const uint Boundary = 0xFFC0C4CC;

        private const uint Mark = 0xFF3B82C4;

        private double _barX;

        private LayoutRect _content;

        public void VisitPage(PageBox page)
        {
            _content = page.Content;
            _barX = Math.Max(4, page.Content.X - 14);
            if (marks.Boundaries) surface.Frame(page.Content, Boundary, 0.5);
        }

        public void VisitLine(LineBox line)
        {
            if (marks.ParagraphSpotlight?.Invoke(line.ParagraphId) is { } tint)
            {
                surface.Fill(new LayoutRect(line.Left, line.Top, Math.Max(line.Line.Width, 4), line.Line.Height), (tint & 0x00FFFFFF) | 0x30000000);
                surface.Fill(new LayoutRect(_content.X - 9, line.Top, 4, line.Line.Height), tint);
            }
            if (marks.FormattingMarks) FormattingMarks(line);
            if (marks.Numbering is { } numbering && marks.LineNumber?.Invoke(line) is { } number && number % Math.Max(1, numbering.Increment) == 0)
                LineNumber(line, number, numbering);
            if (line.Story != StoryKind.Body && line.Story != StoryKind.Cell) return;
            if (marks.ChangeBar?.Invoke(line) is { } color) surface.Line(_barX, line.Top, _barX, line.Top + line.Line.Height, color, 1.5);
        }

        private void FormattingMarks(LineBox line)
        {
            var size = line.Line.Runs.FirstOrDefault()?.Font.Size ?? 12;
            var middle = line.Baseline - size * 0.3;
            foreach (var run in line.Line.Runs)
            {
                var x = line.Left + run.X;
                if (run.Kind == GlyphRunKind.Tab)
                {
                    var end = x + Math.Max(run.Width - 1, 3);
                    surface.Line(x + 1, middle, end, middle, Mark, 0.5);
                    surface.Line(end - 2, middle - 2, end, middle, Mark, 0.5);
                    surface.Line(end - 2, middle + 2, end, middle, Mark, 0.5);
                    continue;
                }
                if (run.Kind != GlyphRunKind.Text) continue;
                for (var index = 0; index < run.Text.Length && index < run.Advances.Count; index++)
                {
                    if (run.Text[index] is ' ' or '\u00A0')
                    {
                        var centre = x + run.Advances[index] / 2;
                        var dot = run.Text[index] == ' ' ? 0.9 : 1.4;
                        surface.Fill(new LayoutRect(centre - dot / 2, middle - dot / 2, dot, dot), Mark);
                    }
                    x += run.Advances[index];
                }
            }
            var last = line.Line.Runs.LastOrDefault();
            var after = line.Left + (last is null ? 0 : last.X + last.Width) + 1.5;
            if (line.Line.IsLast) Pilcrow(after, line.Baseline, size);
            else if (line.Line.Forced) Return(after, middle, size);
        }

        private void Ruby(LayoutGlyphRun run, string ruby, double x, double baseline, uint ink)
        {
            var size = run.Font.Size * 0.5;
            var advances = ruby.Select(character => character >= '\u2E80' ? size : size * 0.55).ToList();
            var width = advances.Sum();
            var glyphs = new LayoutGlyphRun(GlyphRunKind.Text, run.Font with { Size = size }, ruby, 0, 0, 0, advances);
            surface.Glyphs(glyphs, x + (run.Width - width) / 2, baseline - run.Font.Size * 0.95, ink);
        }

        private void FieldLabel(LayoutGlyphRun run, string label, double x, LineBox line)
        {
            var size = Math.Max(5, run.Font.Size * 0.55);
            var advances = label.Select(_ => size * 0.56).ToList();
            var width = advances.Sum() + 3;
            var top = line.Top - size - 1;
            surface.Fill(new LayoutRect(x, top, width, size + 1.5), 0xE6DBEAFE);
            var glyphs = new LayoutGlyphRun(GlyphRunKind.Text, run.Font with { Size = size, Bold = false, Italic = false }, label, 0, 0, 0, advances);
            surface.Glyphs(glyphs, x + 1.5, top + size * 0.85, 0xFF1E40AF);
        }

        private void LineNumber(LineBox line, int number, PulseOffice.Documents.Styles.LineNumbering numbering)
        {
            var font = line.Line.Runs.FirstOrDefault()?.Font ?? new PulseOffice.Documents.Layout.Measurement.FontKey("Liberation Serif", 10, false, false);
            var size = Math.Max(6, font.Size * 0.8);
            var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var advances = text.Select(_ => size * 0.55).ToList();
            var width = advances.Sum();
            var x = numbering.Side == PulseOffice.Documents.Styles.LineNumberSide.Right ? _content.Right + numbering.Distance : _content.X - numbering.Distance - width;
            var run = new LayoutGlyphRun(GlyphRunKind.Text, font with { Size = size, Bold = false, Italic = false }, text, 0, 0, 0, advances);
            surface.Glyphs(run, x, line.Baseline, 0xFF6B7280);
        }

        private void Pilcrow(double x, double baseline, double size)
        {
            var height = size * 0.7;
            var top = baseline - height;
            var bowl = height * 0.45;
            surface.Fill(new LayoutRect(x, top, bowl, bowl), Mark);
            surface.Line(x + bowl, top, x + bowl, baseline, Mark, 0.6);
            surface.Line(x + bowl + 1.6, top, x + bowl + 1.6, baseline, Mark, 0.6);
            surface.Line(x + bowl * 0.5, top + 0.3, x + bowl + 2.2, top + 0.3, Mark, 0.6);
        }

        private void Return(double x, double middle, double size)
        {
            var width = size * 0.5;
            surface.Line(x + width, middle - size * 0.3, x + width, middle, Mark, 0.6);
            surface.Line(x, middle, x + width, middle, Mark, 0.6);
            surface.Line(x, middle, x + 2, middle - 2, Mark, 0.6);
            surface.Line(x, middle, x + 2, middle + 2, Mark, 0.6);
        }

        public void VisitRun(LayoutGlyphRun run, LineBox line)
        {
            if (run.Kind is GlyphRunKind.Object) return;
            var style = run.Style;
            if (style?.Hidden == true && run.Kind is not GlyphRunKind.Label) return;
            var x = line.Left + run.X;
            var baseline = line.Baseline - run.BaselineShift;
            if (marks.FieldShadings && style?.Field is { } field && field != PulseOffice.Text.Rich.TextFieldKind.None && run.Kind == GlyphRunKind.Text)
                surface.Fill(new LayoutRect(x, line.Top, run.Width, line.Line.Height), FieldShade);
            if (style?.Highlight is { } highlight && highlight >> 24 != 0) surface.Fill(new LayoutRect(x, line.Top, run.Width, line.Line.Height), highlight);
            if (run.Kind == GlyphRunKind.Text && style?.Field is { } named && named != PulseOffice.Text.Rich.TextFieldKind.None && marks.FieldName?.Invoke(style) is { Length: > 0 } label)
                FieldLabel(run, label, x, line);
            if (run.Kind == GlyphRunKind.Text && marks.RunSpotlight?.Invoke(line.ParagraphId, run.SourceOffset) is { } spot)
                surface.Fill(new LayoutRect(x, line.Top, run.Width, line.Line.Height), (spot & 0x00FFFFFF) | 0x50000000);
            if (run.Kind == GlyphRunKind.Label && run.Picture is { } picture)
            {
                BulletPicture(picture, x, baseline);
                return;
            }
            var ink = style?.Color ?? Ink;
            if (run.Kind is GlyphRunKind.Text or GlyphRunKind.Label or GlyphRunKind.Hyphen or GlyphRunKind.Citation)
                Text(run, x, baseline, ink);
            if (run.Kind == GlyphRunKind.Text && style?.Ruby is { Length: > 0 } ruby) Ruby(run, ruby, x, baseline, ink);
            if (run.Kind == GlyphRunKind.Tab && run.Leader != PulseOffice.Text.Rich.TabLeader.None)
                surface.Line(x + 1, baseline, x + run.Width - 1, baseline, ink, 0.6, PageStroke.Dotted);
            if (style is null || run.Width <= 0) return;
            var thickness = Math.Max(0.5, run.Font.Size * 0.06);
            if (style.Underline || style.DoubleUnderline)
            {
                var y = baseline + run.Font.Size * 0.12;
                surface.Line(x, y, x + run.Width, y, ink, thickness);
                if (style.DoubleUnderline) surface.Line(x, y + thickness * 2, x + run.Width, y + thickness * 2, ink, thickness);
            }
            if (style.Strikethrough)
            {
                var y = baseline - run.Font.Size * 0.28;
                surface.Line(x, y, x + run.Width, y, ink, thickness);
            }
            if (style.Overline)
            {
                var y = baseline - run.Font.Size * 0.85;
                surface.Line(x, y, x + run.Width, y, ink, thickness);
            }
        }

        private void BulletPicture(PulseOffice.Text.Rich.ListBulletPicture picture, double x, double baseline)
        {
            var bounds = new LayoutRect(x, baseline - picture.Height, picture.Width, picture.Height);
            if (marks.Images) surface.Embedded(bulletPictures.For(picture), bounds, new ObjectTransform(0));
            else surface.Frame(bounds, Boundary, 0.75);
        }

        private void Text(LayoutGlyphRun run, double x, double baseline, uint ink)
        {
            var style = run.Style;
            var offset = Math.Max(0.5, run.Font.Size / 24);
            switch (style?.Relief)
            {
                case PulseOffice.Text.Rich.FontRelief.Embossed:
                    surface.Glyphs(run, x - offset, baseline - offset, Light);
                    surface.Glyphs(run, x + offset, baseline + offset, Dark);
                    break;
                case PulseOffice.Text.Rich.FontRelief.Engraved:
                    surface.Glyphs(run, x - offset, baseline - offset, Dark);
                    surface.Glyphs(run, x + offset, baseline + offset, Light);
                    break;
                default:
                    if (style?.Shadow == true) surface.Glyphs(run, x + offset * 1.5, baseline + offset * 1.5, Dark);
                    break;
            }
            surface.Glyphs(run, x, baseline, style?.Relief is PulseOffice.Text.Rich.FontRelief.Embossed or PulseOffice.Text.Rich.FontRelief.Engraved ? Paper(ink) : ink);
        }

        private uint Paper(uint ink) => ink == Ink ? 0xFFE6E6E6 : ink;

        public void VisitDecoration(DecorationBox decoration)
        {
            var bounds = decoration.Bounds;
            switch (decoration.Kind)
            {
                case DecorationKind.ParagraphBackground or DecorationKind.CellBackground or DecorationKind.PageBackground when decoration.Color is { } fill && fill >> 24 != 0:
                    surface.Fill(bounds, fill);
                    if (decoration.Kind == DecorationKind.PageBackground) surface.Backdrop(fill);
                    break;
                case DecorationKind.FootnoteSeparator or DecorationKind.ColumnSeparator:
                    var horizontal = bounds.Width >= bounds.Height;
                    surface.Line(bounds.X, bounds.Y, horizontal ? bounds.Right : bounds.X, horizontal ? bounds.Y : bounds.Bottom,
                        decoration.Color ?? Ink, Math.Max(0.5, decoration.StrokeWidth));
                    break;
                case DecorationKind.CellBorder when decoration.StrokeWidth > 0:
                    surface.Frame(bounds, decoration.Color ?? Ink, decoration.StrokeWidth);
                    break;
                case DecorationKind.ParagraphBorder or DecorationKind.PageBorder or DecorationKind.CellEdge when decoration.StrokeWidth > 0:
                    Border(decoration);
                    break;
            }
        }

        private void Border(DecorationBox decoration)
        {
            var bounds = decoration.Bounds;
            var ink = decoration.Color ?? Ink;
            var horizontal = bounds.Width >= bounds.Height;
            var width = decoration.StrokeWidth;
            if (decoration.LineStyle == PulseOffice.Text.Rich.BorderLineStyle.Double)
            {
                var thin = Math.Max(0.25, width / 3);
                if (horizontal)
                {
                    surface.Line(bounds.X, bounds.Y + thin / 2, bounds.Right, bounds.Y + thin / 2, ink, thin);
                    surface.Line(bounds.X, bounds.Bottom - thin / 2, bounds.Right, bounds.Bottom - thin / 2, ink, thin);
                }
                else
                {
                    surface.Line(bounds.X + thin / 2, bounds.Y, bounds.X + thin / 2, bounds.Bottom, ink, thin);
                    surface.Line(bounds.Right - thin / 2, bounds.Y, bounds.Right - thin / 2, bounds.Bottom, ink, thin);
                }
                return;
            }
            var stroke = decoration.LineStyle switch
            {
                PulseOffice.Text.Rich.BorderLineStyle.Dotted => PageStroke.Dotted,
                PulseOffice.Text.Rich.BorderLineStyle.Dashed => PageStroke.Dashed,
                _ => PageStroke.Solid,
            };
            if (horizontal) surface.Line(bounds.X, bounds.Y + width / 2, bounds.Right, bounds.Y + width / 2, ink, width, stroke);
            else surface.Line(bounds.X + width / 2, bounds.Y, bounds.X + width / 2, bounds.Bottom, ink, width, stroke);
        }

        public void VisitWatermark(WatermarkBox watermark) =>
            surface.RotatedGlyphs(watermark.Run, watermark.X, watermark.Baseline, watermark.Color, watermark.Angle);

        public void LeaveObject(ObjectBox item) => surface.EndObject();

        public void VisitTurn(TurnedBox turn) => surface.BeginObject(turn.Bounds, turn.RotationDegrees);

        public void LeaveTurn(TurnedBox turn) => surface.EndObject();

        public void VisitObject(ObjectBox item)
        {
            PaintObject(item);
            surface.BeginObject(item.Bounds, item.RotationDegrees);
        }

        private void PaintObject(ObjectBox item)
        {
            if (contents?.Invoke(item.ObjectId) is not { } content) return;
            if (!marks.Images)
            {
                surface.Frame(item.Bounds, Boundary, 0.75);
                surface.Line(item.Bounds.X, item.Bounds.Y, item.Bounds.Right, item.Bounds.Bottom, Boundary, 0.5);
                surface.Line(item.Bounds.Right, item.Bounds.Y, item.Bounds.X, item.Bounds.Bottom, Boundary, 0.5);
                return;
            }
            surface.Embedded(content, item.Bounds, new ObjectTransform(item.RotationDegrees, item.FlipHorizontal, item.FlipVertical));
        }
    }
}
