using System.Globalization;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public enum PageSubset
{
    All,
    Odd,
    Even,
}

public enum SheetOrder
{
    AcrossThenDown,
    DownThenAcross,
}

public enum DuplexMode
{
    None,
    LongEdge,
    ShortEdge,
}

public sealed record PrintJobOptions
{
    public string? PageRange { get; init; }

    public PageSubset Subset { get; init; } = PageSubset.All;

    public bool Reverse { get; init; }

    public int Copies { get; init; } = 1;

    public bool Collate { get; init; } = true;

    public int PagesPerSheet { get; init; } = 1;

    public SheetOrder Order { get; init; } = SheetOrder.AcrossThenDown;

    public bool SheetBorders { get; init; }

    public bool Brochure { get; init; }

    public DuplexMode Duplex { get; init; } = DuplexMode.None;

    public bool Graphics { get; init; } = true;

    public bool BlackText { get; init; }

    public bool Comments { get; init; }
}

public interface IPageRangeParser
{
    IReadOnlyList<int>? Parse(string? range, int pageCount);
}

public sealed class PageRangeParser : IPageRangeParser
{
    public IReadOnlyList<int>? Parse(string? range, int pageCount)
    {
        if (string.IsNullOrWhiteSpace(range)) return [.. Enumerable.Range(0, Math.Max(0, pageCount))];
        var pages = new List<int>();
        foreach (var raw in range.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = raw.IndexOfAny(['-', '–']);
            if (dash < 0)
            {
                if (!Number(raw, out var single)) return null;
                if (single >= 1 && single <= pageCount) pages.Add(single - 1);
                continue;
            }
            var left = raw[..dash].Trim();
            var right = raw[(dash + 1)..].Trim();
            var from = 1;
            var to = pageCount;
            if (left.Length > 0 && !Number(left, out from)) return null;
            if (right.Length > 0 && !Number(right, out to)) return null;
            from = Math.Clamp(from, 0, pageCount + 1);
            to = Math.Clamp(to, 0, pageCount + 1);
            var step = from <= to ? 1 : -1;
            for (var page = from; page != to + step; page += step)
                if (page >= 1 && page <= pageCount) pages.Add(page - 1);
        }
        return pages;
    }

    private bool Number(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}

public interface IPrintImposition
{
    IPrintableDocument Impose(IPrintableDocument document, PrintJobOptions options, bool nativeCopies);

    IReadOnlyList<int> SelectedPages(IPrintableDocument document, PrintJobOptions options);
}

public sealed record SheetSlot(int Page, DocumentRect Area, double Scale, DocumentPoint Offset);

public sealed class PrintImposition(IPageRangeParser ranges) : IPrintImposition
{
    private static readonly int[] Grids = [1, 2, 4, 6, 9, 16];

    private const int MaximumCopies = 999;

    public IReadOnlyList<int> SelectedPages(IPrintableDocument document, PrintJobOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        var pages = ranges.Parse(options.PageRange, document.PageCount) ?? [.. Enumerable.Range(0, document.PageCount)];
        var filtered = options.Subset switch
        {
            PageSubset.Odd => pages.Where(page => page % 2 == 0),
            PageSubset.Even => pages.Where(page => page % 2 == 1),
            _ => pages,
        };
        return [.. filtered];
    }

    public IPrintableDocument Impose(IPrintableDocument document, PrintJobOptions options, bool nativeCopies)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        var source = options.Graphics && !options.BlackText ? document : new FilteredPrintDocument(document, options.Graphics, options.BlackText);
        var pages = SelectedPages(document, options).ToList();
        var page = new DocumentSize(document.Setup.PageWidth, document.Setup.PageHeight);
        List<IReadOnlyList<SheetSlot>> sheets;
        DocumentSize sheet;
        if (options.Brochure)
        {
            sheet = Landscape(page);
            sheets = Brochure(pages, page, sheet);
        }
        else
        {
            var count = Grids.Contains(options.PagesPerSheet) ? options.PagesPerSheet : 1;
            (sheet, var columns, var rows) = Layout(page, count);
            sheets = [.. pages.Chunk(count).Select(chunk => Grid(chunk, page, sheet, columns, rows, options.Order))];
        }
        if (options.Reverse) sheets.Reverse();
        var copies = Math.Clamp(options.Copies, 1, MaximumCopies);
        if (copies > 1 && !nativeCopies)
        {
            sheets = options.Collate
                ? [.. Enumerable.Repeat(sheets, copies).SelectMany(sequence => sequence)]
                : [.. sheets.SelectMany(single => Enumerable.Repeat(single, copies))];
        }
        return new ImposedDocument(source, sheets, sheet, options.SheetBorders && (options.Brochure || options.PagesPerSheet > 1));
    }

    private List<IReadOnlyList<SheetSlot>> Brochure(List<int> pages, DocumentSize page, DocumentSize sheet)
    {
        var padded = (pages.Count + 3) / 4 * 4;
        var half = new DocumentSize(new Twip(sheet.Width.Value / 2), sheet.Height);
        var scale = Math.Min((double)half.Width.Value / page.Width.Value, (double)half.Height.Value / page.Height.Value);
        var sides = new List<IReadOnlyList<SheetSlot>>();
        for (var side = 0; side < padded / 2; side++)
        {
            var (left, right) = side % 2 == 0 ? (padded - 1 - side, side) : (side, padded - 1 - side);
            sides.Add([Slot(left < pages.Count ? pages[left] : -1, 0, half, page, scale), Slot(right < pages.Count ? pages[right] : -1, half.Width.Value, half, page, scale)]);
        }
        return sides;
    }

    private SheetSlot Slot(int source, int left, DocumentSize half, DocumentSize page, double scale)
    {
        var width = (int)(page.Width.Value * scale);
        var height = (int)(page.Height.Value * scale);
        var x = left + (half.Width.Value - width) / 2;
        var y = (half.Height.Value - height) / 2;
        return new SheetSlot(source, new DocumentRect(new Twip(x), new Twip(y), new Twip(width), new Twip(height)), scale, new DocumentPoint(new Twip(x), new Twip(y)));
    }

    private (DocumentSize Sheet, int Columns, int Rows) Layout(DocumentSize page, int count)
    {
        if (count == 1) return (page, 1, 1);
        var best = (Sheet: page, Columns: 1, Rows: count, Scale: 0.0);
        foreach (var sheet in new[] { page, Landscape(page), Portrait(page) })
            for (var columns = 1; columns <= count; columns++)
            {
                if (count % columns != 0) continue;
                var rows = count / columns;
                var scale = Math.Min(sheet.Width.Value / (double)columns / page.Width.Value, sheet.Height.Value / (double)rows / page.Height.Value);
                if (scale > best.Scale + 1e-9) best = (sheet, columns, rows, scale);
            }
        return (best.Sheet, best.Columns, best.Rows);
    }

    private IReadOnlyList<SheetSlot> Grid(int[] chunk, DocumentSize page, DocumentSize sheet, int columns, int rows, SheetOrder order)
    {
        if (columns * rows == 1) return [new SheetSlot(chunk[0], new DocumentRect(Twip.Zero, Twip.Zero, page.Width, page.Height), 1, new DocumentPoint(Twip.Zero, Twip.Zero))];
        var cellWidth = sheet.Width.Value / columns;
        var cellHeight = sheet.Height.Value / rows;
        var gap = 0.94;
        var scale = Math.Min(cellWidth / (double)page.Width.Value, cellHeight / (double)page.Height.Value) * gap;
        var width = (int)(page.Width.Value * scale);
        var height = (int)(page.Height.Value * scale);
        var slots = new List<SheetSlot>();
        for (var index = 0; index < chunk.Length; index++)
        {
            var (column, row) = order == SheetOrder.AcrossThenDown ? (index % columns, index / columns) : (index / rows, index % rows);
            var x = column * cellWidth + (cellWidth - width) / 2;
            var y = row * cellHeight + (cellHeight - height) / 2;
            slots.Add(new SheetSlot(chunk[index], new DocumentRect(new Twip(x), new Twip(y), new Twip(width), new Twip(height)), scale, new DocumentPoint(new Twip(x), new Twip(y))));
        }
        return slots;
    }

    private DocumentSize Landscape(DocumentSize page) =>
        page.Width.Value >= page.Height.Value ? page : new DocumentSize(page.Height, page.Width);

    private DocumentSize Portrait(DocumentSize page) =>
        page.Width.Value <= page.Height.Value ? page : new DocumentSize(page.Height, page.Width);
}

public sealed class ImposedDocument : IPrintableDocument
{
    private readonly IPrintableDocument _source;
    private readonly IReadOnlyList<IReadOnlyList<SheetSlot>> _sheets;
    private readonly bool _borders;

    public ImposedDocument(IPrintableDocument source, IReadOnlyList<IReadOnlyList<SheetSlot>> sheets, DocumentSize sheet, bool borders)
    {
        _source = source;
        _sheets = sheets;
        _borders = borders;
        Setup = new PageSetup
        {
            Paper = new PaperSize("sheet", sheet),
            Margins = Margins.Zero,
            HeaderHeight = Twip.Zero,
            FooterHeight = Twip.Zero,
        };
    }

    public PageSetup Setup { get; }

    public int PageCount => _sheets.Count;

    public IReadOnlyList<IReadOnlyList<SheetSlot>> Sheets => _sheets;

    public void PaintPage(int index, IPageCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (index < 0 || index >= _sheets.Count) return;
        foreach (var slot in _sheets[index])
        {
            if (slot.Page >= 0 && slot.Page < _source.PageCount)
            {
                if (slot.Scale == 1 && slot.Offset.X == Twip.Zero && slot.Offset.Y == Twip.Zero) _source.PaintPage(slot.Page, canvas);
                else
                {
                    var scaled = new ScaledPageCanvas(canvas, slot.Scale, new DocumentPoint(Twip.Zero, Twip.Zero), slot.Offset);
                    _source.PaintPage(slot.Page, new ClippedPageCanvas(scaled, new DocumentRect(Twip.Zero, Twip.Zero, _source.Setup.PageWidth, _source.Setup.PageHeight)));
                }
            }
            if (_borders) Frame(canvas, slot.Area);
        }
    }

    private void Frame(IPageCanvas canvas, DocumentRect area)
    {
        var thin = Twip.FromPoints(0.5);
        var color = new PrintColor(120, 120, 120);
        canvas.DrawLine(new DocumentPoint(area.Left, area.Top), new DocumentPoint(area.Right, area.Top), color, thin);
        canvas.DrawLine(new DocumentPoint(area.Right, area.Top), new DocumentPoint(area.Right, area.Bottom), color, thin);
        canvas.DrawLine(new DocumentPoint(area.Right, area.Bottom), new DocumentPoint(area.Left, area.Bottom), color, thin);
        canvas.DrawLine(new DocumentPoint(area.Left, area.Bottom), new DocumentPoint(area.Left, area.Top), color, thin);
    }
}

public sealed class FilteredPrintDocument(IPrintableDocument source, bool graphics, bool blackText) : IPrintableDocument
{
    public PageSetup Setup => source.Setup;

    public int PageCount => source.PageCount;

    public void PaintPage(int index, IPageCanvas canvas) => source.PaintPage(index, new FilteredPageCanvas(canvas, graphics, blackText));
}

public sealed class FilteredPageCanvas(IPageCanvas inner, bool graphics, bool blackText) : IPageCanvas
{
    public DocumentSize PageSize => inner.PageSize;

    public Twip MeasureText(string text, PrintFont font) => inner.MeasureText(text, font);

    public void FillRectangle(DocumentRect rect, PrintColor color) => inner.FillRectangle(rect, color);

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) => inner.DrawLine(from, to, color, thickness);

    public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) =>
        inner.DrawText(text, bounds, font, blackText ? PrintColor.Black : color, alignment, baseline);

    public void DrawPath(PrintPath path)
    {
        if (graphics) inner.DrawPath(path);
    }

    public void DrawImage(PageImage image, DocumentRect bounds)
    {
        if (graphics) inner.DrawImage(image, bounds);
    }

    public void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        inner.DrawGlyphs(blackText ? run with { Color = PrintColor.Black } : run);
    }

    public IDisposable Clip(DocumentRect area) => inner.Clip(area);

    public void MarkHeading(int level, string title, double topPoints) => inner.MarkHeading(level, title, topPoints);

    public void MarkTarget(string name, double topPoints) => inner.MarkTarget(name, topPoints);

    public void MarkLink(DocumentRect area, string target) => inner.MarkLink(area, target);

    public void MarkNote(DocumentRect area, PrintNote note) => inner.MarkNote(area, note);
}
