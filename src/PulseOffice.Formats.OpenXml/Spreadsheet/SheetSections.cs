using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Book = PulseOffice.Sheets.Workbook;
using ModelCell = PulseOffice.Sheets.Cell;
using OutlineGroup = PulseOffice.Sheets.OutlineGroup;
using Sheet = PulseOffice.Sheets.Worksheet;
using XlsxCell = DocumentFormat.OpenXml.Spreadsheet.Cell;
using XlsxCellValue = DocumentFormat.OpenXml.Spreadsheet.CellValue;
using CellValue = PulseOffice.Sheets.Values.CellValue;

namespace PulseOffice.Formats.OpenXml;

internal sealed class SharedStrings
{
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public SharedStringTable Table { get; } = new();

    public int Intern(string text)
    {
        if (_index.TryGetValue(text, out var existing)) return existing;

        var position = _index.Count;
        Table.AppendChild(new SharedStringItem(new DocumentFormat.OpenXml.Spreadsheet.Text(_escape.Encode(text)) { Space = SpaceProcessingModeValues.Preserve }));
        _index[text] = position;
        return position;
    }

    public IReadOnlyList<string> Read(WorkbookPart part) =>
        part.SharedStringTablePart?.SharedStringTable is { } table ? [.. table.Elements<SharedStringItem>().Select(_codec.TextOf)] : [];

    private readonly XlsxCellCodec _codec = new();
    private readonly OoxmlTextEscape _escape = new();
}

internal sealed class XlsxCellCodec
{
    private readonly TokenMap<CellError> _errors = new(CellError.Value, "#VALUE!",
        (CellError.Null, "#NULL!"), (CellError.DivideByZero, "#DIV/0!"), (CellError.Value, "#VALUE!"),
        (CellError.Reference, "#REF!"), (CellError.Name, "#NAME?"), (CellError.Number, "#NUM!"), (CellError.NotAvailable, "#N/A"),
        (CellError.Spill, "#SPILL!"), (CellError.Calc, "#CALC!"));

    private readonly HashSet<CellError> _withoutFormulaLiteral = [CellError.Spill, CellError.Calc];

    public bool IsFormulaLiteral(CellError error) => !_withoutFormulaLiteral.Contains(error);

    public void WriteValue(XlsxCell cell, CellValue value, SharedStrings strings)
    {
        switch (value.Kind)
        {
            case ValueKind.Number:
                if (!double.IsFinite(value.AsNumber()))
                {
                    cell.DataType = CellValues.Error;
                    cell.CellValue = new XlsxCellValue(_errors.Token(CellError.Number));
                    break;
                }
                cell.DataType = CellValues.Number;
                cell.CellValue = new XlsxCellValue(value.AsNumber().ToString("R", CultureInfo.InvariantCulture));
                break;
            case ValueKind.Boolean:
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new XlsxCellValue(value.AsBoolean() ? "1" : "0");
                break;
            case ValueKind.Text:
                cell.DataType = CellValues.SharedString;
                cell.CellValue = new XlsxCellValue(strings.Intern(value.AsText()).ToString(CultureInfo.InvariantCulture));
                break;
            case ValueKind.Error:
                cell.DataType = CellValues.Error;
                cell.CellValue = new XlsxCellValue(value.ErrorText);
                break;
        }
    }

    public CellValue ReadValue(XlsxCell cell, IReadOnlyList<string> strings)
    {
        var type = cell.DataType?.Value ?? CellValues.Number;

        if (type == CellValues.InlineString)
            return cell.InlineString is { } inline ? CellValue.Text(TextOf(inline)) : CellValue.Empty;

        var text = cell.CellValue?.Text;
        if (string.IsNullOrEmpty(text)) return CellValue.Empty;

        if (type == CellValues.SharedString)
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < strings.Count
                ? CellValue.Text(strings[index])
                : CellValue.Empty;
        if (type == CellValues.String) return CellValue.Text(text);
        if (type == CellValues.Boolean) return CellValue.Boolean(text == "1");
        if (type == CellValues.Error) return CellValue.FromError(_errors.Value(text));

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return CellValue.Text(text);
        return double.IsFinite(number) ? CellValue.Number(number) : CellValue.FromError(CellError.Number);
    }

    public string TextOf(DocumentFormat.OpenXml.OpenXmlCompositeElement item) => _escape.Decode(
        item.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Text>()?.Text
        ?? string.Concat(item.Elements<Run>().Select(run => run.Text?.Text ?? string.Empty)));

    private readonly OoxmlTextEscape _escape = new();
}

internal sealed class OutlineLevels
{
    public (byte Level, bool Hidden)? Of(IReadOnlyList<OutlineGroup> groups, int index)
    {
        var containing = groups.Where(group => group.Contains(index)).ToList();
        return containing.Count == 0 ? null : ((byte)Math.Min(containing.Max(group => group.Level), 7), containing.Any(group => group.IsCollapsed));
    }

    public void Rebuild(Dictionary<int, byte> levels, List<OutlineGroup> groups, Func<int, bool> isHidden)
    {
        var mine = levels.Select(entry => (Index: entry.Key, Value: entry.Value)).OrderBy(entry => entry.Index).ToList();
        if (mine.Count == 0) return;

        var start = mine[0].Index;
        var level = mine[0].Value;
        for (var position = 1; position <= mine.Count; position++)
        {
            var ends = position == mine.Count || mine[position].Value != level || mine[position].Index != mine[position - 1].Index + 1;
            if (!ends) continue;

            var last = mine[position - 1].Index;
            groups.Add(new OutlineGroup { First = start, Last = last, Level = level, IsCollapsed = Enumerable.Range(start, last - start + 1).All(isHidden) });
            if (position == mine.Count) break;

            start = mine[position].Index;
            level = mine[position].Value;
        }
    }
}

internal sealed class XlsxWriteContext(Book workbook, XlsxStyleMapper styles, XlsxConditionalMapper conditionals, SharedStrings strings)
{
    public Book Workbook => workbook;
    public XlsxStyleMapper Styles => styles;
    public XlsxConditionalMapper Conditionals => conditionals;
    public SharedStrings Strings => strings;
    public XlsxCellCodec Cells { get; } = new();
    public OutlineLevels Outlines { get; } = new();
    public XlsxColumnMetrics Columns { get; } = new XlsxColumnMetricsFactory().For(PulseOffice.Sheets.Styling.CellFont.Default);
    public bool UsesDynamicArrays { get; set; }
    public XlsxTablePlan Tables => _tables ??= new XlsxTablePlanner().Plan(workbook);
    private XlsxTablePlan? _tables;
    private readonly XlsxLeapYearSerials _leapYear = new();
    private readonly XlsxCalendarCells _calendar = new(workbook);

    public CellValue ExcelValue(CellValue value, bool formula, int numberFormatId) =>
        !formula && value.Kind == ValueKind.Number && _calendar.HasCalendarFormat(numberFormatId)
            ? CellValue.Number(_leapYear.ToExcel(value.AsNumber()))
            : value;
}

internal abstract class WorksheetSection
{
    public abstract void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context);

    protected void Append(WorksheetPart part, OpenXmlElement? element)
    {
        if (element is not null) part.Worksheet!.AppendChild(element);
    }
}

internal sealed class SheetPropertiesSection : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        var properties = new SheetProperties();
        if (sheet.TabColorHex?.TrimStart('#') is { Length: 6 } colour && colour.All(char.IsAsciiHexDigit))
            properties.Append(new TabColor { Rgb = "FF" + colour.ToUpperInvariant() });
        if (context.Workbook.PageStyles.For(sheet).Setup.FitsToPages)
            properties.Append(new PageSetupProperties { FitToPage = true });
        if (properties.HasChildren) Append(part, properties);
    }
}

internal sealed class SheetViewSection : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        if (!sheet.RightToLeft && !sheet.Frozen.IsFrozen) return;
        var view = new SheetView { WorkbookViewId = 0, RightToLeft = sheet.RightToLeft ? true : null };
        if (sheet.Frozen.IsFrozen)
        {
            var (rows, columns) = (sheet.Frozen.Rows, sheet.Frozen.Columns);
            view.Append(new Pane
            {
                HorizontalSplit = columns > 0 ? columns : null,
                VerticalSplit = rows > 0 ? rows : null,
                TopLeftCell = new CellAddress(rows, columns).ToA1(),
                ActivePane = rows > 0 && columns > 0 ? PaneValues.BottomRight : rows > 0 ? PaneValues.BottomLeft : PaneValues.TopRight,
                State = PaneStateValues.Frozen,
            });
        }
        Append(part, new SheetViews(view));
    }
}

internal sealed class SheetFormatSection : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context) =>
        Append(part, new SheetFormatProperties
        {
            DefaultRowHeight = Math.Round(sheet.Layout.DefaultRowHeight.ToPoints(), 2),
            CustomHeight = sheet.Layout.DefaultRowHeightManual ? true : null,
            DefaultColumnWidth = context.Columns.ToCharacters(sheet.Layout.DefaultColumnWidth),
        });
}

internal sealed class ColumnsSection : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        var widths = sheet.Layout.ColumnWidths;
        var indexes = widths.Keys
            .Concat(sheet.ColumnGroups.SelectMany(group => Enumerable.Range(group.First, group.Count)))
            .Concat(sheet.Layout.ColumnStyles.Keys)
            .Distinct().Order().ToList();
        if (indexes.Count == 0) return;

        var columns = new Columns();
        Column? previous = null;
        var previousIndex = -2;
        foreach (var index in indexes)
        {
            var column = new Column { Min = (uint)(index + 1), Max = (uint)(index + 1) };
            if (widths.TryGetValue(index, out var width) && width.Value > 0)
            {
                column.Width = context.Columns.ToCharacters(width);
                column.CustomWidth = true;
            }
            if (context.Outlines.Of(sheet.ColumnGroups, index) is { } outline)
            {
                column.OutlineLevel = outline.Level;
                if (outline.Hidden) column.Hidden = true;
            }
            if (widths.TryGetValue(index, out var hidden) && hidden.Value == 0) column.Hidden = true;
            if (sheet.Layout.ColumnStyle(index) is { } styleId)
            {
                column.Style = context.Styles.StyleIndexOf(styleId, context.Workbook);
                column.Width ??= context.Columns.ToCharacters(sheet.Layout.DefaultColumnWidth);
            }

            if (previous is not null && previousIndex == index - 1 && SameAs(previous, column))
                previous.Max = (uint)(index + 1);
            else
            {
                columns.AppendChild(column);
                previous = column;
            }
            previousIndex = index;
        }
        Append(part, columns);
    }

    private bool SameAs(Column left, Column right) =>
        left.Width?.Value == right.Width?.Value && left.CustomWidth?.Value == right.CustomWidth?.Value
        && left.Hidden?.Value == right.Hidden?.Value && left.OutlineLevel?.Value == right.OutlineLevel?.Value
        && left.Style?.Value == right.Style?.Value;
}

internal sealed class SheetDataSection(XlsxDataTableWriter dataTables) : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context) => Append(part, new SheetData());

    public void Stream(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        if (part.UnloadRootElement() is not DocumentFormat.OpenXml.Spreadsheet.Worksheet worksheet) return;

        using var writer = OpenXmlWriter.Create(part);
        writer.WriteStartDocument();
        writer.WriteStartElement(worksheet, worksheet.GetAttributes(), worksheet.NamespaceDeclarations);
        foreach (var child in worksheet.ChildElements)
        {
            if (child is not SheetData)
            {
                writer.WriteElement(child);
                continue;
            }

            writer.WriteStartElement(new SheetData());
            foreach (var row in Rows(sheet, context)) writer.WriteElement(row);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private IEnumerable<Row> Rows(Sheet sheet, XlsxWriteContext context)
    {
        var cells = new CellRowIndex(sheet.CellAddresses);
        var tables = dataTables.Plan(sheet);
        var rows = cells.Rows
            .Concat(sheet.Layout.RowHeights.Keys)
            .Concat(sheet.Layout.AutomaticRowHeights.Keys)
            .Concat(sheet.Layout.OptimalRows)
            .Concat(sheet.Layout.RowStyles.Keys)
            .Concat(BlockRows(sheet.Layout.StyleBlocks))
            .Concat(sheet.RowGroups.SelectMany(group => Enumerable.Range(group.First, group.Count)))
            .Distinct()
            .Order();

        foreach (var index in rows)
        {
            var row = new Row { RowIndex = (uint)(index + 1) };
            if (context.Outlines.Of(sheet.RowGroups, index) is { } outline)
            {
                row.OutlineLevel = outline.Level;
                if (outline.Hidden) row.Hidden = true;
            }

            if (sheet.Layout.RowHeights.TryGetValue(index, out var height))
            {
                if (height.Value <= 0)
                {
                    row.Hidden = true;
                    if (sheet.Layout.HiddenRowSize(index) is { } kept)
                    {
                        row.CustomHeight = true;
                        row.Height = kept.ToPoints();
                    }
                    else if (sheet.Layout.AutomaticRowHeights.TryGetValue(index, out var hiddenAutomatic))
                        row.Height = hiddenAutomatic.ToPoints();
                }
                else
                {
                    row.CustomHeight = true;
                    row.Height = height.ToPoints();
                }
            }
            else if (sheet.Layout.AutomaticRowHeights.TryGetValue(index, out var automatic))
                row.Height = automatic.ToPoints();
            else if (sheet.Layout.OptimalRows.Contains(index))
                row.Height = sheet.Layout.DefaultRowHeight.ToPoints();
            else if (sheet.Layout.DefaultRowHeightManual)
            {
                row.CustomHeight = true;
                row.Height = sheet.Layout.DefaultRowHeight.ToPoints();
            }

            if (sheet.Layout.RowStyle(index) is { } rowStyle)
            {
                row.StyleIndex = context.Styles.StyleIndexOf(rowStyle, context.Workbook);
                row.CustomFormat = true;
            }

            var crossings = sheet.Layout.StyleBlocks.IsEmpty ? [] : sheet.Layout.StyleBlocks.RowRuns(index);
            foreach (var column in Columns(cells.Columns(index) ?? [], crossings))
            {
                var address = new CellAddress(index, column);
                if (sheet.TryGetCell(address, out var cell))
                    row.AppendChild(Cell(address, cell with { StyleId = sheet.StyleIdAt(address) }, context, tables));
                else
                    row.AppendChild(new XlsxCell { CellReference = address.ToA1(), StyleIndex = context.Styles.StyleIndexOf(sheet.StyleIdAt(address), context.Workbook) });
            }
            yield return row;
        }
    }

    private IEnumerable<int> BlockRows(StyleBlocks blocks) =>
        blocks.All.SelectMany(block => Enumerable.Range(block.FirstRow, block.LastRow - block.FirstRow + 1));

    private IEnumerable<int> Columns(IReadOnlyList<int> stored, IReadOnlyList<IndexRun<int>> crossings)
    {
        if (crossings.Count == 0) return stored;

        return stored.Concat(crossings.SelectMany(run => Enumerable.Range(run.First, run.Last - run.First + 1))).Distinct().Order();
    }

    private XlsxCell Cell(CellAddress address, ModelCell source, XlsxWriteContext context, XlsxDataTablePlan tables)
    {
        var workbook = context.Workbook;
        var style = workbook.Styles.Get(source.StyleId);
        var inferred = source.HasFormula && style.NumberFormatId == 0 ? source.InferredFormatId : 0;
        if (inferred != 0) style = style with { NumberFormatId = inferred };
        var formatCode = style.NumberFormatId >= PulseOffice.Sheets.Formatting.NumberFormatCatalog.FirstCustomId
            ? workbook.NumberFormats.Get(style.NumberFormatId).Code
            : null;

        var cell = new XlsxCell { CellReference = address.ToA1(), StyleIndex = context.Styles.Map(source.StyleId, style, formatCode, source.StyleName, inferred) };
        if (source.HasFormula && tables.Covers(address, out var table))
        {
            if (table is not null) cell.CellFormula = table;
        }
        else if (source.HasFormula)
        {
            cell.CellFormula = new CellFormula(new XlsxFormulaSyntax().ToOpenXml(source.Formula!));
            if (source.IsArrayAnchor && source.ArrayFormulaRange is { } array)
            {
                cell.CellFormula.FormulaType = CellFormulaValues.Array;
                cell.CellFormula.Reference = $"{array.Start.ToA1()}:{array.End.ToA1()}";
            }
            else if (!source.ImplicitIntersection && (source.SpillRange is not null || source.UsesArrays))
            {
                var spill = source.SpillRange ?? CellRange.Single(address);
                cell.CellFormula.FormulaType = CellFormulaValues.Array;
                cell.CellFormula.Reference = $"{spill.Start.ToA1()}:{spill.End.ToA1()}";
                cell.CellMetaIndex = XlsxDynamicArrays.CellMetadataIndex;
                context.UsesDynamicArrays = true;
            }
        }
        else if (source.Value.Kind == ValueKind.Error && !source.IsPartOfArrayFormula && context.Cells.IsFormulaLiteral(source.Value.Error))
            cell.CellFormula = new CellFormula(source.Value.ErrorText);
        context.Cells.WriteValue(cell, context.ExcelValue(source.Value, source.HasFormula, style.NumberFormatId), context.Strings);
        return cell;
    }
}

internal sealed class ElementSection(Func<Sheet, XlsxWriteContext, OpenXmlElement?> build) : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context) => Append(part, build(sheet, context));
}

internal sealed class ElementsSection(Func<Sheet, XlsxWriteContext, IEnumerable<OpenXmlElement>> build) : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        foreach (var element in build(sheet, context)) Append(part, element);
    }
}

internal sealed class PartSection(Action<WorksheetPart, Sheet, XlsxWriteContext> write) : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context) => write(part, sheet, context);
}

internal sealed class MergesSection : WorksheetSection
{
    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        if (sheet.Merges.Count == 0) return;
        Append(part, new MergeCells(sheet.Merges.All.Select(region => new MergeCell { Reference = region.ToString() })) { Count = (uint)sheet.Merges.Count });
    }
}

internal sealed class HyperlinksSection : WorksheetSection
{
    private readonly XlsxLinkSyntax _links = new();

    public override void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        var linked = sheet.NonEmptyCells().Where(pair => pair.Value.Hyperlink is { Length: > 0 }).ToList();
        if (linked.Count == 0) return;

        var hyperlinks = new Hyperlinks();
        foreach (var (address, cell) in linked)
        {
            var link = cell.Hyperlink!;
            if (_links.IsInternal(link))
                hyperlinks.AppendChild(new Hyperlink { Reference = address.ToA1(), Location = _links.ToLocation(link), Display = Shown(cell, link) });
            else if (_links.External(link) is { } uri)
                hyperlinks.AppendChild(new Hyperlink { Reference = address.ToA1(), Id = part.AddHyperlinkRelationship(uri, true).Id, Display = Shown(cell, link) });
        }
        Append(part, hyperlinks);
    }

    private string Shown(Sheets.Cell cell, string link) =>
        cell.Value.Kind == Sheets.Values.ValueKind.Text && cell.Value.AsText() is { Length: > 0 } text ? text : link;
}

internal sealed class XlsxReadContext(Book workbook, IReadOnlyList<string> strings, IReadOnlyList<int> styleMap, IReadOnlyList<string?> styleNames, Stylesheet? stylesheet, List<Diagnostic> diagnostics)
{
    public Book Workbook => workbook;
    public IReadOnlyList<string> Strings => strings;
    public IReadOnlyList<int> StyleMap => styleMap;
    public IReadOnlyList<string?> StyleNames => styleNames;
    public Stylesheet? Stylesheet => stylesheet;
    public List<Diagnostic> Diagnostics => diagnostics;
    public XlsxCellCodec Cells { get; } = new();
    public OutlineLevels Outlines { get; } = new();
    public XlsxColumnMetrics Columns { get; } = new XlsxColumnMetricsFactory().For(stylesheet);
    public ConstantFormulas Constants { get; } = new();
    public XlsxTableCatalog Tables { get; } = new();
    public StructuredReferenceResolver References => _references ??= new StructuredReferenceResolver(Tables);
    private StructuredReferenceResolver? _references;
    public IReadOnlySet<uint> DynamicArrays { get; init; } = new HashSet<uint>();
}

internal interface IXlsxSheetPartReader
{
    void Read(WorksheetPart part, Sheet sheet, XlsxReadContext context);
}

internal sealed class SharedFormulaTable(PulseOffice.Sheets.Engine.Operations.FormulaTranslator translator)
{
    private readonly Dictionary<uint, (CellAddress Anchor, string Formula)> _masters = [];

    public string? Resolve(CellFormula? element, CellAddress address, string? own)
    {
        if (element?.FormulaType?.Value != CellFormulaValues.Shared || element.SharedIndex?.Value is not { } index) return own;
        if (own is not null)
        {
            _masters.TryAdd(index, (address, own));
            return own;
        }
        if (!_masters.TryGetValue(index, out var master)) return null;
        return translator.Translate(master.Formula, address.Row - master.Anchor.Row, address.Column - master.Anchor.Column) ?? master.Formula;
    }
}

internal sealed record XlsxDataTable(CellRange Range, bool TwoDimensional, bool RowInput, string First, string? Second);

internal sealed class XlsxDataTables(XlsxFormulaSyntax formulas)
{
    private readonly List<XlsxDataTable> _tables = [];
    private readonly OccupiedCells _occupied = new();

    public void Collect(CellFormula? element, CellAddress address)
    {
        if (element?.FormulaType?.Value != CellFormulaValues.DataTable) return;
        if (element.Reference?.Value is not { } reference || !CellRange.TryParseA1(reference, out var range) || range.Start != address) return;
        if (element.R1?.Value is not { Length: > 0 } first || range.Start.Row == 0 || range.Start.Column == 0) return;
        var twoDimensional = element.DataTable2D?.Value == true;
        if (twoDimensional && element.R2?.Value is not { Length: > 0 }) return;
        _tables.Add(new XlsxDataTable(range, twoDimensional, element.DataTableRow?.Value == true, Absolute(first), element.R2?.Value is { } second ? Absolute(second) : null));
    }

    public void Apply(Sheet target)
    {
        foreach (var table in _tables)
            foreach (var address in _occupied.Clip(table.Range, target).Cells())
                target[address] = target[address] with { Formula = formulas.ToInternal(Operation(table, address)) };
    }

    private string Operation(XlsxDataTable table, CellAddress cell)
    {
        var top = table.Range.Start.Row - 1;
        var left = table.Range.Start.Column - 1;
        var column = CellAddress.ColumnName(cell.Column);
        var leftColumn = CellAddress.ColumnName(left);
        var row = (cell.Row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var topRow = (top + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (table.TwoDimensional)
            return $"MULTIPLE.OPERATIONS(${leftColumn}${topRow},{table.Second},${leftColumn}{row},{table.First},{column}${topRow})";
        return table.RowInput
            ? $"MULTIPLE.OPERATIONS(${leftColumn}{row},{table.First},{column}${topRow})"
            : $"MULTIPLE.OPERATIONS({column}${topRow},{table.First},${leftColumn}{row})";
    }

    private string Absolute(string reference) =>
        CellAddress.TryParseA1(reference.Replace("$", string.Empty, StringComparison.Ordinal), out var address) ? address.ToA1(true, true) : reference;
}

internal sealed class SheetCellsState(SharedFormulaTable shared)
{
    public XlsxDataTables DataTables { get; } = new(new XlsxFormulaSyntax());

    public Dictionary<int, byte> RowLevels { get; } = [];

    public HashSet<CellRange> Arrays { get; } = [];

    public HashSet<CellRange> Spills { get; } = [];

    public SharedFormulaTable Shared => shared;

    public int LastRow { get; set; } = -1;
}

internal sealed class SheetDataReader(OoxmlAttributeSanitizer sanitizer) : IXlsxSheetPartReader
{
    private const double MaximumRowPoints = 409;
    private const double MaximumColumnCharacters = 255;
    private const byte MaximumOutlineLevel = 7;

    private readonly OccupiedCells _occupied = new();

    public void Read(WorksheetPart part, Sheet target, XlsxReadContext context)
    {
        var columnLevels = new Dictionary<int, byte>();
        var state = new SheetCellsState(new SharedFormulaTable(new PulseOffice.Sheets.Engine.Operations.FormulaTranslator()));
        var worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet();
        var laidOut = false;

        using (var reader = OpenXmlReader.Create(part))
        {
            if (!reader.Read() || reader.ElementType != typeof(DocumentFormat.OpenXml.Spreadsheet.Worksheet)) return;
            foreach (var declaration in reader.NamespaceDeclarations) worksheet.AddNamespaceDeclaration(declaration.Key, declaration.Value);
            worksheet.SetAttributes(reader.Attributes);

            if (reader.ReadFirstChild())
            {
                do
                {
                    if (!reader.IsStartElement) continue;
                    if (reader.ElementType != typeof(SheetData))
                    {
                        var child = reader.LoadCurrentElement();
                        if (child is null) continue;
                        sanitizer.SanitizeTree(child);
                        worksheet.AppendChild(child);
                        continue;
                    }

                    Layout(worksheet, target, columnLevels, context);
                    laidOut = true;
                    worksheet.AppendChild(new SheetData());
                    if (!reader.ReadFirstChild()) continue;
                    do
                    {
                        if (reader.IsStartElement && reader.ElementType == typeof(Row) && reader.LoadCurrentElement() is Row row)
                            ReadRow(row, target, state, context);
                    }
                    while (reader.ReadNextSibling());
                }
                while (reader.ReadNextSibling());
            }
        }

        if (!laidOut) Layout(worksheet, target, columnLevels, context);
        var light = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(worksheet.OuterXml);
        sanitizer.SanitizeTree(light);
        part.Worksheet = light;

        state.DataTables.Apply(target);
        foreach (var array in state.Arrays.Where(array => !state.Spills.Contains(array)))
            foreach (var member in Occupied(array, target))
                target[member] = target[member] with { ArrayFormulaRange = array, IsArrayAnchor = member == array.Start, ImplicitIntersection = false };
        foreach (var spill in state.Spills)
            foreach (var member in Occupied(spill, target))
                target[member] = member == spill.Start
                    ? target[member] with { SpillRange = spill.CellCount > 1 ? spill : null, ImplicitIntersection = false }
                    : target[member] with { IsSpilled = !target[member].HasFormula };

        context.Outlines.Rebuild(state.RowLevels, target.RowGroups, target.Layout.IsRowHidden);
        context.Outlines.Rebuild(columnLevels, target.ColumnGroups, target.Layout.IsColumnHidden);
    }

    private IEnumerable<CellAddress> Occupied(CellRange range, Sheet target) => _occupied.Clip(range, target).Cells();

    private void Layout(DocumentFormat.OpenXml.Spreadsheet.Worksheet worksheet, Sheet target, Dictionary<int, byte> columnLevels, XlsxReadContext context)
    {
        Defaults(worksheet, target, context);
        Columns(worksheet, target, columnLevels, context);
    }

    private void ReadRow(Row row, Sheet target, SheetCellsState state, XlsxReadContext context)
    {
        var rowIndex = uint.TryParse(row.RowIndex?.InnerText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var explicitRow) && explicitRow > 0 ? (int)Math.Min(explicitRow, (uint)CellAddress.MaxRows + 1) - 1 : state.LastRow + 1;
        if (rowIndex < 0 || rowIndex >= CellAddress.MaxRows) return;
        state.LastRow = rowIndex;
        try
        {
            ReadCells(row, rowIndex, target, state, context);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException or InvalidOperationException)
        {
            sanitizer.SanitizeTree(row);
            ReadCells(row, rowIndex, target, state, context);
        }
    }

    private void ReadCells(Row row, int rowIndex, Sheet target, SheetCellsState state, XlsxReadContext context)
    {
        var arrays = state.Arrays;
        var spills = state.Spills;
        RowLayout(row, rowIndex, target, state.RowLevels, context);
        var column = -1;
        foreach (var cell in row.Elements<XlsxCell>())
        {
            CellAddress address;
            if (cell.CellReference?.Value is not { } reference)
                address = new CellAddress(rowIndex, column + 1);
            else if (!CellAddress.TryParseA1(reference, out address))
            {
                context.Diagnostics.Add(Diagnostic.Warning("XLSX041", $"Invalid reference: '{reference}'.").With(reference));
                continue;
            }
            if (!address.IsValid) continue;
            column = address.Column;

            var styleIndex = (int)(cell.StyleIndex?.Value ?? 0);
            var value = context.Cells.ReadValue(cell, context.Strings);
            if (Crossing(row, cell, value, styleIndex, address, target, context)) continue;
            var own = cell.CellFormula?.Text is { Length: > 0 } text ? new XlsxFormulaSyntax().ToInternal(context.References.Resolve(text, address, target.Name)) : null;
            var formula = state.Shared.Resolve(cell.CellFormula, address, own);
            state.DataTables.Collect(cell.CellFormula, address);
            target[address] = new ModelCell
            {
                Value = value,
                StyleId = styleIndex >= 0 && styleIndex < context.StyleMap.Count ? context.StyleMap[styleIndex] : 0,
                ExplicitStyle = true,
                StyleName = styleIndex >= 0 && styleIndex < context.StyleNames.Count ? context.StyleNames[styleIndex] : null,
                Formula = context.Constants.IsConstant(value, formula) ? null : formula,
                ImplicitIntersection = formula is not null && !(cell.CellMetaIndex?.Value is { } dynamicIndex && context.DynamicArrays.Contains(dynamicIndex)),
            };
            if (cell.CellFormula?.FormulaType?.Value == CellFormulaValues.Array
                && cell.CellFormula.Reference?.Value is { } arrayReference
                && CellRange.TryParseA1(arrayReference, out var array)
                && array.Start == address
                && arrays.Add(array))
            {
                if (cell.CellMetaIndex?.Value is { } spillIndex && context.DynamicArrays.Contains(spillIndex)) spills.Add(array);
            }
        }
    }

    private bool Crossing(Row row, XlsxCell cell, CellValue value, int styleIndex, CellAddress address, Sheet target, XlsxReadContext context)
    {
        if (row.CustomFormat?.Value != true || row.StyleIndex is null || !value.IsEmpty || cell.CellFormula is not null
            || (styleIndex >= 0 && styleIndex < context.StyleNames.Count && context.StyleNames[styleIndex] is not null)
            || target.TryGetCell(address, out _))
            return false;

        var styleId = StyleOf(styleIndex, context);
        if (target.Layout.Styles.LineStyleAt(address) != styleId) target.Layout.SetStyleBlock(CellRange.Single(address), styleId);
        return true;
    }

    private void Defaults(DocumentFormat.OpenXml.Spreadsheet.Worksheet worksheet, Sheet target, XlsxReadContext context)
    {
        if (worksheet.GetFirstChild<SheetFormatProperties>() is not { } format)
        {
            target.Layout.DefaultColumnWidth = context.Columns.FromCharacters(8.43);
            return;
        }

        if (format.DefaultRowHeight?.Value is { } height && height > 0) target.Layout.DefaultRowHeight = Twip.FromPoints(Math.Min(height, MaximumRowPoints));
        target.Layout.DefaultRowHeightManual = format.CustomHeight?.Value == true;
        target.Layout.DefaultColumnWidth = format.DefaultColumnWidth?.Value is { } width && width > 0
            ? context.Columns.FromCharacters(Math.Min(width, MaximumColumnCharacters))
            : context.Columns.FromCharacters(Math.Min(format.BaseColumnWidth?.Value ?? 8, MaximumColumnCharacters) + 0.43);
    }

    private int StyleOf(int styleIndex, XlsxReadContext context) =>
        styleIndex >= 0 && styleIndex < context.StyleMap.Count ? context.StyleMap[styleIndex] : 0;

    private void Columns(DocumentFormat.OpenXml.Spreadsheet.Worksheet worksheet, Sheet target, Dictionary<int, byte> levels, XlsxReadContext context)
    {
        if (worksheet.GetFirstChild<Columns>() is not { } columns) return;

        var free = 0L;
        foreach (var column in columns.Elements<Column>().OrderBy(column => column.Min?.Value ?? 1))
        {
            var first = (int)Math.Max(free, (long)(column.Min?.Value ?? 1) - 1);
            var last = (int)Math.Min(column.Max?.Value ?? 1, (uint)CellAddress.MaxColumns) - 1;
            free = Math.Max(free, (long)last + 1);
            for (var index = first; index <= last && index >= 0; index++)
            {
                if (column.OutlineLevel?.Value is { } outline && outline > 0) levels[index] = Math.Min(outline, MaximumOutlineLevel);
                if (column.Style?.Value is { } styleIndex && index < CellAddress.MaxColumns)
                    target.Layout.SetColumnStyle(index, StyleOf((int)styleIndex, context));
                if (column.Hidden?.Value == true)
                {
                    target.Layout.SetColumnWidth(index, Twip.Zero);
                    continue;
                }
                if (column.Width?.Value is { } read && read > 0 && Math.Min(read, MaximumColumnCharacters) is var width
                    && (column.CustomWidth?.Value == true || Math.Abs(context.Columns.FromCharacters(width).Value - target.Layout.DefaultColumnWidth.Value) > 20))
                    target.Layout.SetColumnWidth(index, context.Columns.FromCharacters(width));
            }
        }
    }

    private void RowLayout(Row row, int index, Sheet target, Dictionary<int, byte> levels, XlsxReadContext context)
    {
        if (row.Height?.Value is { } read && read > 0 && Math.Min(read, MaximumRowPoints) is var height && row.CustomHeight?.Value == true)
        {
            if (!target.Layout.DefaultRowHeightManual || Twip.FromPoints(height) != target.Layout.DefaultRowHeight)
                target.Layout.SetRowHeight(index, Twip.FromPoints(height));
        }
        else if (row.Height?.Value is { } automatic && automatic > 0) target.Layout.SetAutomaticRowHeight(index, Twip.FromPoints(Math.Min(automatic, MaximumRowPoints)));
        if (row.Hidden?.Value == true) target.Layout.HideRow(index);

        if (row.OutlineLevel?.Value is { } level && level > 0) levels[index] = Math.Min(level, MaximumOutlineLevel);

        if (row.CustomFormat?.Value == true && row.StyleIndex?.Value is { } styleIndex)
            target.Layout.SetRowStyle(index, StyleOf((int)styleIndex, context));
    }
}

internal sealed class HyperlinksReader : IXlsxSheetPartReader
{
    private readonly XlsxLinkSyntax _links = new();
    private readonly HyperlinkTargets _targets = new();

    public void Read(WorksheetPart part, Sheet sheet, XlsxReadContext context)
    {
        if (part.Worksheet?.GetFirstChild<Hyperlinks>() is not { } hyperlinks) return;

        foreach (var hyperlink in hyperlinks.Elements<Hyperlink>())
        {
            if (hyperlink.Reference?.Value is not { } reference || !CellAddress.TryParseA1(reference.Split(':')[0], out var address)) continue;

            var target = hyperlink.Id?.Value is { } id
                ? _targets.Of(part, id)
                : hyperlink.Location?.Value is { } location ? _links.FromLocation(location) : null;
            if (target is null) continue;

            sheet[address] = sheet[address] with { Hyperlink = target };
        }
    }
}

internal sealed class SheetPartReader(Action<WorksheetPart, Sheet, XlsxReadContext> read) : IXlsxSheetPartReader
{
    public void Read(WorksheetPart part, Sheet sheet, XlsxReadContext context) => read(part, sheet, context);
}
