using DocumentFormat.OpenXml;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxDeckTableMapper
{
    private readonly PptxTableStyleResolver? _styles;

    public PptxDeckTableMapper()
    {
    }

    internal PptxDeckTableMapper(PptxTableStyleResolver styles) => _styles = styles;

    private readonly OptimalColumnSizing _optimal = new(new PulseOffice.Document.Tables.ColumnWidths());
    private const string TableUri = "http://schemas.openxmlformats.org/drawingml/2006/table";

    public P.GraphicFrame Build(ObjectElement element, DataTableObject table, uint id)
    {
        var snapshot = table.Snapshot;
        var columns = snapshot.Columns.Count;
        var fractions = table.Layout.ColumnFractions.Count == columns && columns > 0
            ? table.Layout.ColumnFractions
            : _optimal.Apply(table).ColumnFractions;
        var frameWidth = element.Bounds.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip;

        var tbl = new D.Table(new D.TableProperties
        {
            FirstRow = table.Style.HeaderRow,
            BandRow = table.Style.BandedRows,
        });

        var grid = new D.TableGrid();
        for (var c = 0; c < columns; c++)
            grid.Append(new D.GridColumn { Width = (long)Math.Round(frameWidth * (c < fractions.Count ? fractions[c] : 1.0 / columns)) });
        tbl.Append(grid);

        var cells = new TableCellIndex(table);
        if (table.Style.HeaderRow && columns > 0)
            tbl.Append(BodyRow([.. snapshot.Columns.Select(col => (object?)col.Name)], TableCellFormat.HeaderRow, columns, table, cells, Height(table, 0)));

        var offset = table.Style.HeaderRow && columns > 0 ? 1 : 0;
        for (var r = 0; r < snapshot.Rows.Count; r++)
            tbl.Append(BodyRow(snapshot.Rows[r], r, columns, table, cells, Height(table, r + offset)));

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Table" },
                new P.NonVisualGraphicFrameDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new D.Offset { X = element.Bounds.Left.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Y = element.Bounds.Top.Value * PulseOffice.Foundation.Units.Emu.PerTwip },
                new D.Extents { Cx = element.Bounds.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Cy = element.Bounds.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip }),
            new D.Graphic(new D.GraphicData(tbl) { Uri = TableUri }));
    }

    private long Height(DataTableObject table, int displayed) =>
        (long)Math.Round(TableMetrics.RowHeight(table, displayed) * ((double)PulseOffice.Foundation.Units.Emu.PerPoint));

    private D.TableRow BodyRow(
        IReadOnlyList<object?> cells, int rowIndex, int columns, DataTableObject table, TableCellIndex index, long height)
    {
        var row = new D.TableRow { Height = height };

        for (var c = 0; c < columns; c++)
        {
            var text = c < cells.Count ? cells[c]?.ToString() ?? string.Empty : string.Empty;
            var format = index.Format(rowIndex, c);
            var cell = Cell(text, format?.VerticalAlign ?? CellVerticalAlign.Top, table.Style.OwnLook ? format?.Look ?? TableCellLook.Plain : null);

            if (index.Origin(rowIndex, c) is { } origin)
            {
                if (origin.ColumnSpan > 1) cell.GridSpan = origin.ColumnSpan;
                if (origin.RowSpan > 1) cell.RowSpan = origin.RowSpan;
            }
            else if (index.Cover(rowIndex, c) is { } cover)
            {
                if (c > cover.Column) cell.HorizontalMerge = true;
                if (rowIndex > cover.Row) cell.VerticalMerge = true;
            }

            row.Append(cell);
        }

        return row;
    }

    private D.TableCell Cell(string text, CellVerticalAlign valign, TableCellLook? look)
    {
        var properties = new D.TableCellProperties();
        if (valign == CellVerticalAlign.Center) properties.Anchor = D.TextAnchoringTypeValues.Center;
        else if (valign == CellVerticalAlign.Bottom) properties.Anchor = D.TextAnchoringTypeValues.Bottom;
        if (look is not null)
        {
            properties.Append(
                Border(new D.LeftBorderLineProperties(), look.Left),
                Border(new D.RightBorderLineProperties(), look.Right),
                Border(new D.TopBorderLineProperties(), look.Top),
                Border(new D.BottomBorderLineProperties(), look.Bottom));
            properties.Append(look.FillHex is { } fill ? new D.SolidFill(Rgb(fill)) : new D.NoFill());
        }

        var body = new D.TextBody(new D.BodyProperties(), new D.ListStyle());
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var paragraph = new D.Paragraph();
            if (look?.Horizontal is { } horizontal)
                paragraph.Append(new D.ParagraphProperties
                {
                    Alignment = horizontal switch
                    {
                        CellHorizontalAlign.Center => D.TextAlignmentTypeValues.Center,
                        CellHorizontalAlign.Right => D.TextAlignmentTypeValues.Right,
                        _ => D.TextAlignmentTypeValues.Left,
                    },
                });
            paragraph.Append(line.Length == 0 ? new D.EndParagraphRunProperties() : new D.Run(RunProperties(look), new D.Text(line)));
            body.Append(paragraph);
        }
        return new D.TableCell(body, properties);
    }

    private D.RunProperties RunProperties(TableCellLook? look)
    {
        var run = new D.RunProperties();
        if (look is null) return run;
        if (look.FontSize is { } size) run.FontSize = (int)Math.Round(size * 100);
        run.Bold = look.Bold;
        if (look.Italic) run.Italic = true;
        if (look.Underline) run.Underline = D.TextUnderlineValues.Single;
        if (look.Strikethrough) run.Strike = D.TextStrikeValues.SingleStrike;
        if (look.TextHex is { } ink) run.Append(new D.SolidFill(Rgb(ink)));
        if (look.FontFamily is { Length: > 0 } family) run.Append(new D.LatinFont { Typeface = family });
        return run;
    }

    private TBorder Border<TBorder>(TBorder border, TableBorderLine? line) where TBorder : OpenXmlCompositeElement
    {
        if (line is null)
        {
            border.Append(new D.NoFill());
            return border;
        }
        border.SetAttribute(new OpenXmlAttribute("w", string.Empty, ((long)Math.Round(line.WidthPoints * ((double)PulseOffice.Foundation.Units.Emu.PerPoint))).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        border.Append(new D.SolidFill(Rgb(line.ColorHex)));
        return border;
    }

    private D.RgbColorModelHex Rgb(string hex) => new() { Val = hex.TrimStart('#').ToUpperInvariant() };

    public ObjectElement? Read(P.GraphicFrame frame, string id) => Read(frame, id, null);

    internal ObjectElement? Read(P.GraphicFrame frame, string id, DocumentFormat.OpenXml.Packaging.PresentationPart? presentation)
    {
        var tbl = frame.Graphic?.GraphicData?.GetFirstChild<D.Table>();
        if (tbl is null) return null;

        var rows = tbl.Elements<D.TableRow>().Take(ImportedTableLimits.SlideTableRows).ToList();
        if (rows.Count == 0) return null;

        var firstRow = tbl.TableProperties?.FirstRow?.Value ?? false;
        var bandRow = tbl.TableProperties?.BandRow?.Value ?? false;
        var gridCount = tbl.TableGrid?.Elements<D.GridColumn>().Take(ImportedTableLimits.SlideTableColumns).Count() ?? 0;
        var columnCount = Math.Max(1, Math.Max(gridCount, rows.Max(row => Cells(row).Count())));
        rows = rows.Take(Math.Max(1, ImportedTableLimits.SlideTableCells / columnCount)).ToList();
        var headerCells = Cells(rows[0]).Select(CellText).ToList();

        var columns = Enumerable.Range(0, columnCount)
            .Select(i => new DataColumn(firstRow && i < headerCells.Count ? headerCells[i] : string.Empty, DataValueKind.Text))
            .ToList();

        var dataRows = (firstRow ? rows.Skip(1) : rows)
            .Select(r =>
            {
                var values = Cells(r).Select(CellText).Cast<object?>().ToList();
                while (values.Count < columnCount) values.Add(string.Empty);
                return (IReadOnlyList<object?>)values;
            })
            .ToList();

        var merges = new List<TableMerge>();
        var cellFormats = new List<TableCellFormat>();
        var headerShift = firstRow ? 1 : 0;
        for (var displayed = 0; displayed < rows.Count; displayed++)
        {
            var r = displayed - headerShift;
            var cellElements = Cells(rows[displayed]).ToList();
            for (var c = 0; c < cellElements.Count; c++)
            {
                if (cellElements[c].HorizontalMerge?.Value == true || cellElements[c].VerticalMerge?.Value == true) continue;
                var colSpan = Math.Clamp(cellElements[c].GridSpan?.Value ?? 1, 1, cellElements.Count - c);
                var rowSpan = Math.Clamp(cellElements[c].RowSpan?.Value ?? 1, 1, rows.Count - displayed);
                if (colSpan > 1 || rowSpan > 1) merges.Add(new TableMerge(r, c, rowSpan, colSpan));

                if (r >= 0 && Format(cellElements[c], r, c) is { } format) cellFormats.Add(format);
            }
        }

        var ownLook = _styles is not null && _styles.HasLook(tbl, presentation);
        if (ownLook)
        {
            var style = _styles!.Style(tbl, presentation);
            var properties = tbl.TableProperties;
            var flags = new TableFlags(firstRow, properties?.LastRow?.Value ?? false, properties?.FirstColumn?.Value ?? false,
                properties?.LastColumn?.Value ?? false, bandRow, properties?.BandColumn?.Value ?? false);
            cellFormats.Clear();
            for (var displayed = 0; displayed < rows.Count; displayed++)
            {
                var row = displayed - (firstRow ? 1 : 0);
                var cells = Cells(rows[displayed]).ToList();
                for (var c = 0; c < cells.Count && c < columnCount; c++)
                {
                    var look = _styles.Look(style, flags, cells[c], displayed, c, rows.Count, columnCount, presentation?.SlideMasterParts.FirstOrDefault()?.ThemePart);
                    cellFormats.Add((Format(cells[c], row, c) ?? new TableCellFormat(row, c, CellVerticalAlign.Top)) with { Look = look });
                }
            }
        }

        var bounds = ReadBounds(frame.Transform);
        var content = new DataTableObject
        {
            Id = id,
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = columns, Rows = dataRows },
            Style = new DataTableStyle { HeaderRow = firstRow, BandedRows = bandRow, OwnLook = ownLook, Grid = !ownLook },
            Merges = merges,
            CellFormats = cellFormats,
            Layout = ReadLayout(tbl, rows),
        };

        return new ObjectElement { Id = id, Bounds = bounds, Content = content };
    }

    private IEnumerable<D.TableCell> Cells(D.TableRow row) => row.Elements<D.TableCell>().Take(ImportedTableLimits.SlideTableColumns);

    private TableCellFormat? Format(D.TableCell cell, int row, int column)
    {
        var anchor = cell.TableCellProperties?.Anchor?.Value;
        if (anchor == D.TextAnchoringTypeValues.Center) return new TableCellFormat(row, column, CellVerticalAlign.Center);
        if (anchor == D.TextAnchoringTypeValues.Bottom) return new TableCellFormat(row, column, CellVerticalAlign.Bottom);
        return null;
    }

    private TableLayout ReadLayout(D.Table tbl, IReadOnlyList<D.TableRow> rows)
    {
        var widths = tbl.TableGrid?.Elements<D.GridColumn>().Take(ImportedTableLimits.SlideTableColumns).Select(g => (double)(g.Width?.Value ?? 0)).ToList() ?? [];
        var heights = rows.Select(r => (r.Height?.Value ?? 0) / ((double)PulseOffice.Foundation.Units.Emu.PerPoint)).ToList();
        var total = widths.Sum();
        return new TableLayout
        {
            ColumnFractions = widths.Count > 0 && widths.All(w => w > 0) ? [.. widths.Select(w => w / total)] : [],
            RowHeights = heights.Count > 0 && heights.All(h => h > 0) ? [.. heights.Select(h => Math.Round(h, 2))] : [],
        };
    }

    private string CellText(D.TableCell cell) =>
        string.Join("\n", cell.TextBody?.Elements<D.Paragraph>().Select(paragraph => string.Concat(paragraph.ChildElements.Select(child => child switch
        {
            D.Run run => run.Text?.Text ?? string.Empty,
            D.Field field => field.Text?.Text ?? string.Empty,
            D.Break => "\n",
            _ => string.Empty,
        }))) ?? []);

    private DocumentRect ReadBounds(P.Transform? xfrm)
    {
        var offset = xfrm?.Offset;
        var extents = xfrm?.Extents;
        return new DocumentRect(
            new Twip((int)((offset?.X?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            new Twip((int)((offset?.Y?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            new Twip((int)((extents?.Cx?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            new Twip((int)((extents?.Cy?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)));
    }
}
