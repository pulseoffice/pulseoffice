using PulseOffice.Drawing.Printing;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Printing;

public readonly record struct SheetPage(BandSlice Columns, BandSlice Rows, int Number);

public sealed class SheetPrintDocument : IPrintableDocument
{
    private readonly Worksheet _sheet;
    private readonly StyleCatalog _styles;
    private readonly NumberFormatCatalog _builtInFormats = new();
    private readonly ICellPresenter _presenter;
    private readonly IHeaderFooterFields _fields;
    private readonly SheetPrintOptions _options;
    private readonly List<SheetPage> _pages = [];
    private readonly DateTimeOffset _printedAt;
    private readonly IConditionalAppearanceSource _conditional;
    private readonly ConditionalCellPainter _conditionalPainter;

    private const double IndentPoints = 6;
    private static readonly Twip HeadingHeight = Twip.FromPoints(12);
    private static readonly Twip HeadingWidth = Twip.FromPoints(28);
    private static readonly PrintFont HeadingFont = new("Helvetica", 8);
    private static readonly PrintColor HeadingBackground = new(238, 241, 246);

    public SheetPrintDocument(
        Worksheet sheet,
        StyleCatalog styles,
        ICellPresenter presenter,
        IHeaderFooterFields fields,
        SheetPrintOptions options,
        TimeProvider? time = null,
        IConditionalAppearanceSource? conditional = null,
        ConditionalCellPainter? conditionalPainter = null,
        ISheetObjectExtent? extent = null,
        IObjectPathBuilder? paths = null,
        ISheetObjectImages? images = null,
        IJobPageNumbering? numbering = null)
    {
        _numbering = numbering ?? new OwnPageNumbering(options?.Setup.FirstPageNumber);
        _images = images ?? new NoSheetObjectImages();
        _paths = paths ?? new ObjectPathBuilder();
        _extent = extent ?? new SheetObjectExtent();
        _conditional = conditional ?? new NoConditionalAppearance();
        _conditionalPainter = conditionalPainter ?? new ConditionalCellPainter();
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _printedAt = (time ?? TimeProvider.System).GetLocalNow();

        _area = ResolvedArea();
        _scale = EffectiveScale();
        Paginate();
    }

    private readonly double _scale = 1;

    public double Scale => _scale;

    private double EffectiveScale()
    {
        var requested = double.IsFinite(Setup.Scale) && Setup.Scale > 0 ? Math.Clamp(Setup.Scale, 0.1, 4) : 1;
        if (!Setup.FitsToPages || Area is not { } area) return requested;

        var content = Setup.ContentArea;
        var headingWidth = Setup.PrintHeadings ? HeadingWidth.Value : 0;
        var headingHeight = Setup.PrintHeadings ? HeadingHeight.Value : 0;
        var fit = 1.0;

        if (Setup.FitToWidthPages is > 0 and var across)
        {
            var total = VisibleSize(area.Start.Column, area.End.Column, _sheet.Layout.IsColumnHidden, _sheet.Layout.ColumnWidth);
            if (total > 0) fit = Math.Min(fit, (double)across * (content.Width.Value - headingWidth) / total);
        }

        if (Setup.FitToHeightPages is > 0 and var down)
        {
            var total = VisibleSize(area.Start.Row, area.End.Row, _sheet.Layout.IsRowHidden, _sheet.Layout.RowHeight);
            if (total > 0) fit = Math.Min(fit, (double)down * (content.Height.Value - headingHeight) / total);
        }

        return Math.Clamp(fit * 0.999, 0.1, 1);
    }

    private static long VisibleSize(int first, int last, Func<int, bool> hidden, Func<int, Twip> size)
    {
        long total = 0;

        for (var index = first; index <= last; index++)
            if (!hidden(index))
                total += size(index).Value;

        return total;
    }

    public PageSetup Setup => _options.Setup;

    public int PageCount => _pages.Count;

    public IReadOnlyList<SheetPage> Pages => _pages;

    private readonly ISheetObjectExtent _extent;

    private readonly CellRange? _area;

    private CellRange? Area => _area;

    private CellRange? ResolvedArea()
    {
        if (_options.Area is not { } requested) return PrintedRange();
        if (!requested.IsWholeColumns && !requested.IsWholeRows) return requested;
        if (PrintedRange() is not { } used || !used.IntersectsWith(requested)) return null;
        return new CellRange(
            new CellAddress(
                requested.IsWholeColumns ? Math.Max(requested.Start.Row, used.Start.Row) : requested.Start.Row,
                requested.IsWholeRows ? Math.Max(requested.Start.Column, used.Start.Column) : requested.Start.Column),
            new CellAddress(
                requested.IsWholeColumns ? Math.Min(requested.End.Row, used.End.Row) : requested.End.Row,
                requested.IsWholeRows ? Math.Min(requested.End.Column, used.End.Column) : requested.End.Column));
    }

    private CellRange? PrintedRange()
    {
        var cells = _sheet.UsedRange();
        var objects = _extent.CoverOfAll(_sheet);

        if (cells is not { } used) return objects;
        if (objects is not { } cover) return used;

        return new CellRange(
            new CellAddress(
                Math.Min(used.Start.Row, cover.Start.Row),
                Math.Min(used.Start.Column, cover.Start.Column)),
            new CellAddress(
                Math.Max(used.End.Row, cover.End.Row),
                Math.Max(used.End.Column, cover.End.Column)));
    }

    private void Paginate()
    {
        if (Area is not { } area) return;

        var paginator = new BandPaginator();
        var content = ContentArea();

        var columns = paginator.Split(
            new ColumnBand(_sheet.Layout, area.Start.Column, area.End.Column, _options.ColumnBreaks),
            content.Width, RepeatedColumns(area));

        var rows = paginator.Split(
            new RowBand(_sheet.Layout, area.Start.Row, area.End.Row, _options.RowBreaks),
            content.Height, RepeatedRows(area));

        if (columns.Count == 0 || rows.Count == 0) return;

        var number = 1;

        if (Setup.Order == PageOrder.DownThenAcross)
            foreach (var column in columns)
                foreach (var row in rows)
                    _pages.Add(new SheetPage(column, row, number++));
        else
            foreach (var row in rows)
                foreach (var column in columns)
                    _pages.Add(new SheetPage(column, row, number++));
    }

    private DocumentRect ContentArea()
    {
        var physical = Setup.ContentArea;
        var area = new DocumentRect(
            physical.Left, physical.Top,
            new Twip((int)(physical.Width.Value / _scale)), new Twip((int)(physical.Height.Value / _scale)));
        if (!Setup.PrintHeadings) return area;

        return new DocumentRect(
            area.Left + HeadingWidth, area.Top + HeadingHeight,
            area.Width - HeadingWidth, area.Height - HeadingHeight);
    }

    private readonly IObjectPathBuilder _paths;
    private readonly ISheetObjectImages _images;
    private readonly IJobPageNumbering _numbering;

    public void PaintPage(int index, IPageCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (index >= _pages.Count) return;

        var page = _pages[index];
        var numbered = _numbering.For(this, index);

        PaintBand(canvas, Setup.HeaderArea, Setup.HeaderOn(numbered.StartsSection, numbered.Number), numbered, TextBaseline.Top);
        PaintBand(canvas, Setup.FooterArea, Setup.FooterOn(numbered.StartsSection, numbered.Number), numbered, TextBaseline.Bottom);

        var origin = ContentArea();
        canvas = new ScaledPageCanvas(canvas, _scale,
            new DocumentPoint(Setup.ContentArea.Left, Setup.ContentArea.Top), Centring(page));

        if (Setup.PrintHeadings) PaintHeadings(canvas, page, origin);

        var y = origin.Top;
        var firstRow = true;

        var rowTops = new Dictionary<int, Twip>();
        var columnLefts = new Dictionary<int, Twip>();
        var rows = RowsOf(page).ToList();
        var columns = ColumnsOf(page).ToList();

        foreach (var row in rows)
        {
            var height = _sheet.Layout.RowHeight(row);
            var x = origin.Left;
            var firstColumn = true;

            rowTops[row] = y;

            for (var position = 0; position < columns.Count; position++)
            {
                var column = columns[position];
                var width = _sheet.Layout.ColumnWidth(column);
                columnLefts[column] = x;

                var address = new CellAddress(row, column);

                if (_sheet.Merges.At(address) is { } region)
                {
                    if (IsFirstOnPage(region, address, rows, columns))
                    {
                        var visible = new DocumentRect(
                            x, y, SpanOnPage(columns, region.Start.Column, region.End.Column, ColumnWidthOf),
                            SpanOnPage(rows, region.Start.Row, region.End.Row, RowHeightOf));

                        var whole = new DocumentRect(
                            x - Visible(region.Start.Column, column - 1, _sheet.Layout.IsColumnHidden, ColumnWidthOf),
                            y - Visible(region.Start.Row, row - 1, _sheet.Layout.IsRowHidden, RowHeightOf),
                            Visible(region.Start.Column, region.End.Column, _sheet.Layout.IsColumnHidden, ColumnWidthOf),
                            Visible(region.Start.Row, region.End.Row, _sheet.Layout.IsRowHidden, RowHeightOf));

                        PaintCell(new ClippedPageCanvas(canvas, visible), region.Start, whole, firstRow, firstColumn, []);
                    }
                }
                else
                {
                    PaintCell(canvas, address, new DocumentRect(x, y, width, height), firstRow, firstColumn,
                        columns.Skip(position + 1));
                }

                firstColumn = false;
                x += width;
            }

            firstRow = false;
            y += height;
        }

        PaintObjects(canvas, page, rows, columns, rowTops, columnLefts, origin);
    }

    private DocumentPoint Centring(SheetPage page)
    {
        var content = Setup.ContentArea;
        var x = 0;
        var y = 0;

        if (Setup.CenterHorizontally)
        {
            var used = ColumnsOf(page).Sum(column => (long)_sheet.Layout.ColumnWidth(column).Value)
                + (Setup.PrintHeadings ? HeadingWidth.Value : 0);
            x = (int)Math.Max(0, (content.Width.Value - used * _scale) / 2);
        }

        if (Setup.CenterVertically)
        {
            var used = RowsOf(page).Sum(row => (long)_sheet.Layout.RowHeight(row).Value)
                + (Setup.PrintHeadings ? HeadingHeight.Value : 0);
            y = (int)Math.Max(0, (content.Height.Value - used * _scale) / 2);
        }

        return new DocumentPoint(new Twip(x), new Twip(y));
    }

    private Twip ColumnWidthOf(int column) => _sheet.Layout.ColumnWidth(column);

    private Twip RowHeightOf(int row) => _sheet.Layout.RowHeight(row);

    private static bool IsFirstOnPage(
        CellRange region, CellAddress address, IReadOnlyList<int> rows, IReadOnlyList<int> columns) =>
        address.Row == rows.First(row => row >= region.Start.Row && row <= region.End.Row)
        && address.Column == columns.First(column => column >= region.Start.Column && column <= region.End.Column);

    private static Twip Visible(int first, int last, Func<int, bool> hidden, Func<int, Twip> size)
    {
        var total = Twip.Zero;

        for (var index = first; index <= last; index++)
            if (!hidden(index))
                total += size(index);

        return total;
    }

    private static Twip SpanOnPage(IReadOnlyList<int> indices, int first, int last, Func<int, Twip> size)
    {
        var total = Twip.Zero;

        foreach (var index in indices)
            if (index >= first && index <= last)
                total += size(index);

        return total;
    }

    private void PaintObjects(
        IPageCanvas canvas, SheetPage page, IReadOnlyList<int> rows, IReadOnlyList<int> columns,
        IReadOnlyDictionary<int, Twip> rowTops, IReadOnlyDictionary<int, Twip> columnLefts, DocumentRect origin)
    {
        if (_sheet.Objects.Count == 0) return;

        var bodyRow = rows.FirstOrDefault(row => row >= page.Rows.First, -1);
        var bodyColumn = columns.FirstOrDefault(column => column >= page.Columns.First, -1);
        if (bodyRow < 0 || bodyColumn < 0) return;

        var bodyTop = rowTops[bodyRow];
        var bodyLeft = columnLefts[bodyColumn];
        var body = new DocumentRect(
            bodyLeft, bodyTop,
            origin.Left + SpanOnPage(columns, 0, int.MaxValue, ColumnWidthOf) - bodyLeft,
            origin.Top + SpanOnPage(rows, 0, int.MaxValue, RowHeightOf) - bodyTop);
        var rowBase = _sheet.Layout.RowOffset(page.Rows.First).Value;
        var columnBase = _sheet.Layout.ColumnOffset(page.Columns.First).Value;

        foreach (var placed in _sheet.Objects.OrderBy(placed => placed.ZOrder))
        {
            var bounds = new DocumentRect(
                bodyLeft + new Twip(_sheet.Layout.ColumnOffset(placed.Anchor.Column).Value - columnBase) + placed.OffsetX,
                bodyTop + new Twip(_sheet.Layout.RowOffset(placed.Anchor.Row).Value - rowBase) + placed.OffsetY,
                placed.Size.Width, placed.Size.Height);

            if (bounds.IsEmpty || !bounds.IntersectsWith(body)) continue;

            var target = Inside(bounds, body) ? canvas : new ClippedPageCanvas(canvas, body);
            if (_images.Image(placed.Content, bounds) is { } image) target.DrawImage(image.Image, image.Area);
            else _paths.Paint(target, placed.Content, bounds);
        }
    }

    private static bool Inside(DocumentRect shape, DocumentRect area) =>
        shape.Left.Value >= area.Left.Value && shape.Right.Value <= area.Right.Value
        && shape.Top.Value >= area.Top.Value && shape.Bottom.Value <= area.Bottom.Value;

    private BandSlice? RepeatedRows(CellRange area) =>
        _options.RepeatedRowRange
        ?? (_options.RepeatedRows > 0
            ? new BandSlice(area.Start.Row, Math.Min(area.End.Row, area.Start.Row + _options.RepeatedRows - 1))
            : null);

    private BandSlice? RepeatedColumns(CellRange area) =>
        _options.RepeatedColumnRange
        ?? (_options.RepeatedColumns > 0
            ? new BandSlice(area.Start.Column, Math.Min(area.End.Column, area.Start.Column + _options.RepeatedColumns - 1))
            : null);

    private IEnumerable<int> RowsOf(SheetPage page)
    {
        if (Area is not { } area) yield break;

        if (RepeatedRows(area) is { } titles)
            for (var row = titles.First; row <= titles.Last; row++)
                if (!_sheet.Layout.IsRowHidden(row) && row < page.Rows.First)
                    yield return row;

        for (var row = page.Rows.First; row <= page.Rows.Last; row++)
            if (!_sheet.Layout.IsRowHidden(row))
                yield return row;
    }

    private IEnumerable<int> ColumnsOf(SheetPage page)
    {
        if (Area is not { } area) yield break;

        if (RepeatedColumns(area) is { } titles)
            for (var column = titles.First; column <= titles.Last; column++)
                if (!_sheet.Layout.IsColumnHidden(column) && column < page.Columns.First)
                    yield return column;

        for (var column = page.Columns.First; column <= page.Columns.Last; column++)
            if (!_sheet.Layout.IsColumnHidden(column))
                yield return column;
    }

    private void PaintHeadings(IPageCanvas canvas, SheetPage page, DocumentRect origin)
    {
        var x = origin.Left;

        foreach (var column in ColumnsOf(page))
        {
            var width = _sheet.Layout.ColumnWidth(column);
            var bounds = new DocumentRect(x, origin.Top - HeadingHeight, width, HeadingHeight);

            canvas.FillRectangle(bounds, HeadingBackground);
            canvas.DrawText(CellAddress.ColumnName(column), bounds, HeadingFont, PrintColor.Black,
                TextAlignment.Center);

            x += width;
        }

        var y = origin.Top;

        foreach (var row in RowsOf(page))
        {
            var height = _sheet.Layout.RowHeight(row);
            var bounds = new DocumentRect(origin.Left - HeadingWidth, y, HeadingWidth, height);

            canvas.FillRectangle(bounds, HeadingBackground);
            canvas.DrawText((row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                bounds, HeadingFont, PrintColor.Black, TextAlignment.Center);

            y += height;
        }
    }

    private void PaintCell(
        IPageCanvas canvas, CellAddress address, DocumentRect bounds, bool firstRow, bool firstColumn,
        IEnumerable<int> followingColumns)
    {
        var cell = _sheet[address] with { StyleId = _sheet.StyleIdAt(address) };
        var appearance = _conditional.At(_sheet, address);
        var style = appearance.Over(_styles.Get(cell.StyleId));

        if (style.BackgroundHex is { } background)
            canvas.FillRectangle(bounds, PrintColor.FromHex(background));

        _conditionalPainter.PaintFill(canvas, appearance, bounds);

        if (Setup.PrintGridLines) PaintGridLines(canvas, bounds, firstRow, firstColumn);

        PaintBorders(canvas, style, bounds);

        _conditionalPainter.PaintIcon(canvas, appearance, bounds);
        if (appearance.HideValue) return;
        if (appearance.IconIndex is not null)
        {
            var reserve = _conditionalPainter.IconReserve(bounds);
            bounds = bounds with { Left = bounds.Left + reserve, Width = bounds.Width - reserve };
        }

        var formatted = _presenter.Present(cell, _styles, _sheet.Owner?.NumberFormats ?? _builtInFormats, CharactersIn(bounds.Width, style));
        if (formatted.Text.Length == 0) return;

        var font = new PrintFont(style.Font.Family, style.Font.Size, style.Font.Bold, style.Font.Italic);
        var color = PrintColor.FromName(formatted.ColorName) ?? PrintColor.FromHex(style.Font.ColorHex);

        var padding = Twip.FromPoints(2 + Math.Max(style.Indent, 0) * IndentPoints);

        var available = SpilledWidth(address, style, bounds.Width, followingColumns);

        var text = new DocumentRect(
            bounds.Left + padding, bounds.Top, available - padding * 2, bounds.Height);

        canvas.DrawText(formatted.Text, text, font, color, Align(style, formatted), Baseline(style));
    }

    private Twip SpilledWidth(CellAddress address, CellStyle style, Twip width, IEnumerable<int> followingColumns)
    {
        if (style.WrapText || style.Horizontal is CellHorizontalAlignment.Right) return width;
        if (_sheet.GetValue(address).Kind != Values.ValueKind.Text) return width;

        var total = width.Value;
        var expected = address.Column + 1;

        foreach (var column in followingColumns.Take(32))
        {
            if (column < expected) break;

            for (; expected < column; expected++)
                if (!_sheet.Layout.IsColumnHidden(expected)) return new Twip(total);

            var next = new CellAddress(address.Row, column);
            if (!_sheet.IsBlank(next) || _sheet.Merges.At(next) is not null) break;

            total += _sheet.Layout.ColumnWidth(column).Value;
            expected = column + 1;
        }

        return new Twip(total);
    }

    private static TextAlignment Align(CellStyle style, FormattedCell formatted) => style.Horizontal switch
    {
        CellHorizontalAlignment.Left => TextAlignment.Left,
        CellHorizontalAlignment.Center => TextAlignment.Center,
        CellHorizontalAlignment.Right => TextAlignment.Right,
        _ => formatted.AlignRight ? TextAlignment.Right : TextAlignment.Left,
    };

    private static TextBaseline Baseline(CellStyle style) => style.Vertical switch
    {
        CellVerticalAlignment.Top => TextBaseline.Top,
        CellVerticalAlignment.Middle => TextBaseline.Middle,
        _ => TextBaseline.Bottom,
    };

    private static double CharactersIn(Twip width, CellStyle style) =>
        Math.Max(width.ToPoints() / Math.Max(style.Font.Size * 0.55, 1), 1);

    private static void PaintGridLines(IPageCanvas canvas, DocumentRect bounds, bool firstRow, bool firstColumn)
    {
        var thickness = Twip.FromPoints(0.25);

        canvas.DrawLine(
            new DocumentPoint(bounds.Left, bounds.Bottom),
            new DocumentPoint(bounds.Right, bounds.Bottom), PrintColor.Grid, thickness);

        canvas.DrawLine(
            new DocumentPoint(bounds.Right, bounds.Top),
            new DocumentPoint(bounds.Right, bounds.Bottom), PrintColor.Grid, thickness);

        if (firstRow)
            canvas.DrawLine(
                new DocumentPoint(bounds.Left, bounds.Top),
                new DocumentPoint(bounds.Right, bounds.Top), PrintColor.Grid, thickness);

        if (firstColumn)
            canvas.DrawLine(
                new DocumentPoint(bounds.Left, bounds.Top),
                new DocumentPoint(bounds.Left, bounds.Bottom), PrintColor.Grid, thickness);
    }

    private static void PaintBorders(IPageCanvas canvas, CellStyle style, DocumentRect bounds)
    {
        if (style.Borders.IsEmpty) return;

        Edge(style.Borders.Top, new DocumentPoint(bounds.Left, bounds.Top), new DocumentPoint(bounds.Right, bounds.Top));
        Edge(style.Borders.Bottom,
            new DocumentPoint(bounds.Left, bounds.Bottom), new DocumentPoint(bounds.Right, bounds.Bottom));
        Edge(style.Borders.Left,
            new DocumentPoint(bounds.Left, bounds.Top), new DocumentPoint(bounds.Left, bounds.Bottom));
        Edge(style.Borders.Right,
            new DocumentPoint(bounds.Right, bounds.Top), new DocumentPoint(bounds.Right, bounds.Bottom));
        Edge(style.Borders.DiagonalUp,
            new DocumentPoint(bounds.Left, bounds.Bottom), new DocumentPoint(bounds.Right, bounds.Top));
        Edge(style.Borders.DiagonalDown,
            new DocumentPoint(bounds.Left, bounds.Top), new DocumentPoint(bounds.Right, bounds.Bottom));

        void Edge(BorderEdge edge, DocumentPoint from, DocumentPoint to)
        {
            if (!edge.IsVisible) return;
            canvas.DrawLine(from, to, PrintColor.FromHex(edge.ColorHex), edge.Width);
        }
    }

    private void PaintBand(
        IPageCanvas canvas, DocumentRect area, HeaderFooter band, JobPage page, TextBaseline baseline)
    {
        if (band.IsEmpty) return;

        var context = new PageContext(
            page.Number, page.Total, _options.DocumentName, _sheet.Name, _printedAt);

        var font = new PrintFont("Helvetica", 9);

        Segment(band.Left, TextAlignment.Left);
        Segment(band.Center, TextAlignment.Center);
        Segment(band.Right, TextAlignment.Right);

        void Segment(string pattern, TextAlignment alignment)
        {
            if (string.IsNullOrEmpty(pattern)) return;

            canvas.DrawText(_fields.Expand(pattern, context), area, font, PrintColor.Black, alignment, baseline);
        }
    }
}
