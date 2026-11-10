using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Formats.Packaging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsReadContext(OdfPackage package, XDocument content, Workbook workbook, List<Diagnostic> diagnostics)
{
    public OdfPackage Package => package;
    public XDocument Content => content;
    public XDocument? Styles { get; } = package.ReadXml("styles.xml");
    public Workbook Workbook => workbook;
    public List<Diagnostic> Diagnostics => diagnostics;
    public OdsRepeatBudget Budget { get; } = new(diagnostics);

    public IReadOnlyDictionary<string, int> StyleIds { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, CellStyle> NamedStyles { get; set; } = new Dictionary<string, CellStyle>();
    public IReadOnlyDictionary<string, string> Parents { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> NamedStyleNames { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, Twip> ColumnWidths { get; set; } = new Dictionary<string, Twip>();
    public IReadOnlyDictionary<string, Twip> RowHeights { get; set; } = new Dictionary<string, Twip>();

    public IReadOnlySet<string> OptimalRows { get; set; } = new HashSet<string>();
    public IReadOnlySet<string> RowBreakStyles { get; set; } = new HashSet<string>();
    public IReadOnlyDictionary<string, string> StyleLinks { get; set; } = new Dictionary<string, string>();
    public IReadOnlySet<string> ColumnBreakStyles { get; set; } = new HashSet<string>();
}

internal interface IOdsSheetPartReader
{
    void Read(XElement table, Worksheet sheet, OdsReadContext context);
}

internal sealed class ObjectPartReader(Func<XElement, OdsReadContext, IEnumerable<PlacedObject>> read) : IOdsSheetPartReader
{
    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var placed in read(table, context)) sheet.Objects.Add(placed);
    }
}

internal sealed class ConditionalPartReader(OdfConditionalMapper conditionals) : IOdsSheetPartReader
{
    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var rule in conditionals.ReadRules(table, context.NamedStyles)) sheet.ConditionalRules.Add(rule);
    }
}

internal sealed class AnchoringPartReader : IOdsSheetPartReader
{
    private readonly HashSet<XName> _cells = [Namespaces.Table + "table-cell", Namespaces.Table + "covered-table-cell"];
    private readonly XName _shapes = Namespaces.Table + "shapes";
    private readonly XName _link = Namespaces.Draw + "a";

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        if (sheet.Objects.Count == 0) return;

        var modes = new Dictionary<string, ObjectAnchoring>(StringComparer.Ordinal);
        foreach (var element in table.Descendants())
        {
            if ((string?)element.Attribute(Namespaces.Draw + "name") is not { } name) continue;
            var holder = element.Parent?.Name == _link ? element.Parent.Parent : element.Parent;
            if (holder is null) continue;

            if (holder.Name == _shapes) modes.TryAdd(name, ObjectAnchoring.Fixed);
            else if (_cells.Contains(holder.Name))
                modes.TryAdd(name, element.Attribute(Namespaces.Table + "end-cell-address") is null ? ObjectAnchoring.Move : ObjectAnchoring.MoveAndSize);
        }

        for (var index = 0; index < sheet.Objects.Count; index++)
            if (modes.TryGetValue(sheet.Objects[index].Id, out var mode))
                sheet.Objects[index] = sheet.Objects[index] with { Anchoring = mode };
    }
}

internal sealed class AltTextPartReader(OdfAltTextMapper altText) : IOdsSheetPartReader
{
    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        var described = altText.ReadByName(table);
        for (var i = 0; i < sheet.Objects.Count; i++)
            if (described.TryGetValue(sheet.Objects[i].Id, out var text))
                sheet.Objects[i] = sheet.Objects[i] with { Content = text.ApplyTo(sheet.Objects[i].Content) };
    }
}

internal sealed class CommentPartReader(TableCells cells, IOdfParagraphText paragraphs) : IOdsSheetPartReader
{
    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var cell in cells.In(table))
        {
            if (cell.Element.Element(Namespaces.Office + "annotation") is not { } annotation) continue;

            var text = paragraphs.Read(annotation);
            if (text.Length > 0)
                sheet.Comments[cell.Address] = new CellComment
                {
                    Text = text,
                    Author = annotation.Element(Namespaces.Dc + "creator")?.Value,
                    IsAlwaysVisible = annotation.Attribute(Namespaces.Office + "display")?.Value == "true",
                    CreatedAt = DateTimeOffset.TryParse(annotation.Element(Namespaces.Dc + "date")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) ? created : DateTimeOffset.Now,
                };
        }
    }
}

internal sealed class ArrayFormulaPartReader(TableCells cells, TableRows rows) : IOdsSheetPartReader
{
    private readonly OdfDynamicArrays _dynamicArrays = new();

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var cell in cells.In(table))
        {
            if (cell.Element.Attribute(Namespaces.Table + "number-matrix-columns-spanned") is null
                && cell.Element.Attribute(Namespaces.Table + "number-matrix-rows-spanned") is null) continue;

            if (!cell.Address.IsValid || !sheet[cell.Address].HasFormula) continue;
            var columns = context.Budget.Columns(cell.Address.Column, rows.Repeat(cell.Element, Namespaces.Table + "number-matrix-columns-spanned"));
            var height = context.Budget.Rows(cell.Address.Row, rows.Repeat(cell.Element, Namespaces.Table + "number-matrix-rows-spanned"));
            if (context.Budget.TakeCellRows(height, columns) < height) continue;
            var range = new CellRange(cell.Address, new CellAddress(cell.Address.Row + height - 1, cell.Address.Column + columns - 1));

            if (_dynamicArrays.IsMarked(cell.Element))
            {
                foreach (var member in range.Cells())
                    sheet[member] = member == range.Start
                        ? sheet[member] with { SpillRange = range.CellCount > 1 ? range : null, ImplicitIntersection = false }
                        : sheet[member] with { IsSpilled = !sheet[member].HasFormula };
                continue;
            }

            foreach (var member in range.Cells())
                sheet[member] = sheet[member] with { ArrayFormulaRange = range, IsArrayAnchor = member == range.Start, ImplicitIntersection = false };
        }
    }
}

internal sealed class MergePartReader(TableCells cells, TableRows rows) : IOdsSheetPartReader
{
    private readonly OverlappingMerges _overlapping = new("ODF191");

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var cell in cells.In(table))
        {
            var columns = rows.Repeat(cell.Element, Namespaces.Table + "number-columns-spanned");
            var spanned = rows.Repeat(cell.Element, Namespaces.Table + "number-rows-spanned");
            if (columns <= 1 && spanned <= 1) continue;

            var end = new CellAddress(
                Math.Min(cell.Address.Row + spanned - 1, CellAddress.MaxRows - 1),
                Math.Min(cell.Address.Column + columns - 1, CellAddress.MaxColumns - 1));
            _overlapping.Add(sheet.Merges, new CellRange(cell.Address, end), context.Diagnostics);
        }
    }
}

internal sealed class SparklinePartReader(OdfSparklineMapper sparklines) : IOdsSheetPartReader
{
    public void Read(XElement table, Worksheet sheet, OdsReadContext context) => sparklines.Read(table, sheet);
}

internal sealed class PrintRangePartReader(TableRows rows) : IOdsSheetPartReader
{
    private string Cell(string text)
    {
        var dot = text.LastIndexOf('.');
        return (dot >= 0 ? text[(dot + 1)..] : text).Replace("$", string.Empty, StringComparison.Ordinal);
    }

    private IEnumerable<string> Tokens(string text)
    {
        var current = new System.Text.StringBuilder();
        var quoted = false;

        foreach (var c in text)
        {
            if (c == '\'') quoted = !quoted;
            if (c == ' ' && !quoted)
            {
                if (current.Length > 0) yield return current.ToString();
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) yield return current.ToString();
    }

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        if (table.Element(Namespaces.Table + "table-header-rows") is { } header)
            sheet.RepeatedPrintRows = (int)Math.Min(rows.In(new XElement(Namespaces.Table + "table", header.Elements())).Sum(row => (long)row.Repeat), CellAddress.MaxRows);
        if (table.Element(Namespaces.Table + "table-header-columns") is { } headerColumns)
            sheet.RepeatedPrintColumns = (int)Math.Min(rows.Columns(headerColumns).Sum(column => (long)rows.Repeat(column, Namespaces.Table + "number-columns-repeated")), CellAddress.MaxColumns);

        var declared = table.Attribute(Namespaces.Table + "print-ranges")?.Value;
        if (string.IsNullOrWhiteSpace(declared)) return;

        foreach (var part in Tokens(declared))
        {
            var internalText = new OdfFormulaSyntax().ToInternalExpression("[" + part + "]");
            var colon = internalText.LastIndexOf(':');
            var startText = Cell(colon < 0 ? internalText : internalText[..colon]);
            var reference = colon < 0 ? startText : Cell(internalText[(colon + 1)..]);

            if (CellAddress.TryParseA1(startText, out var start) && CellAddress.TryParseA1(reference, out var end))
                sheet.PrintRanges.Add(new CellRange(start, end));
        }
    }
}

internal sealed class AnchorNormalisingPartReader(ISheetPlacement placement) : IOdsSheetPartReader
{
    private readonly CellAddress _origin = new(0, 0);

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        for (var index = 0; index < sheet.Objects.Count; index++)
        {
            var placed = sheet.Objects[index];
            if (Fits(sheet.Layout, placed)) continue;
            sheet.Objects[index] = placement.Place(sheet.Layout, _origin, placed, placement.Box(sheet.Layout, _origin, placed));
        }
    }

    private bool Fits(SheetLayout layout, PlacedObject placed) =>
        Within(placed.OffsetX.Value, layout.ColumnWidth(placed.Anchor.Column).Value)
        && Within(placed.OffsetY.Value, layout.RowHeight(placed.Anchor.Row).Value);

    private bool Within(double offset, double extent) => offset == 0 || (offset > 0 && offset < extent);
}

internal sealed class LayoutPartReader(TableRows rows, OdfLayoutMapper layout) : IOdsSheetPartReader
{
    private bool ManualDefault(XElement table, Twip height, OdsReadContext context)
    {
        long manual = 0, optimal = 0;
        foreach (var row in rows.In(table))
        {
            if (row.Element.Attribute(Namespaces.Table + "style-name")?.Value is not { } style
                || !context.RowHeights.TryGetValue(style, out var size) || size != height) continue;
            if (context.OptimalRows.Contains(style)) optimal += row.Repeat;
            else manual += row.Repeat;
        }
        return manual > optimal;
    }

    private int LastOf(int first, int repeat, int limit) => (int)Math.Min((long)first + Math.Max(repeat, 1) - 1, limit - 1);

    private const int MaximumRepeatedBreaks = 4096;

    private IEnumerable<int> Breaks(int first, int repeat, int limit) =>
        Enumerable.Range(first, Math.Min(LastOf(first, Math.Min(repeat, MaximumRepeatedBreaks), limit) - first + 1, MaximumRepeatedBreaks));

    private bool Collapsed(XElement element) =>
        (string?)element.Attribute(Namespaces.Table + "visibility") is "collapse" or "filter";

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        var columnWidths = rows.Columns(table)
            .Select(column => (Size: column.Attribute(Namespaces.Table + "style-name")?.Value is { } name && context.ColumnWidths.TryGetValue(name, out var size) ? size : (Twip?)null,
                Repeat: rows.Repeat(column, Namespaces.Table + "number-columns-repeated"))).ToList();
        var declared = columnWidths.Sum(span => (long)span.Repeat);
        if (declared < PulseOffice.Sheets.Addressing.CellAddress.MaxColumns)
            columnWidths.Add((null, (int)(PulseOffice.Sheets.Addressing.CellAddress.MaxColumns - declared)));
        if (Prevailing(columnWidths) is { } defaultWidth) sheet.Layout.DefaultColumnWidth = defaultWidth;
        var rowHeights = rows.In(table)
            .Select(row => (Size: row.Element.Attribute(Namespaces.Table + "style-name")?.Value is { } name && context.RowHeights.TryGetValue(name, out var size) ? size : (Twip?)null,
                Repeat: row.Repeat));
        if (Prevailing(rowHeights) is { } defaultHeight) sheet.Layout.DefaultRowHeight = defaultHeight;
        sheet.Layout.DefaultRowHeightManual = ManualDefault(table, sheet.Layout.DefaultRowHeight, context);

        var columnIndex = 0;
        foreach (var column in rows.Columns(table))
        {
            var repeat = rows.Repeat(column, Namespaces.Table + "number-columns-repeated");
            if (column.Attribute(Namespaces.Table + "style-name")?.Value is { } style && context.ColumnWidths.TryGetValue(style, out var width)
                && width != sheet.Layout.DefaultColumnWidth)
                sheet.Layout.SetColumnWidthRange(columnIndex, LastOf(columnIndex, repeat, CellAddress.MaxColumns), width);
            if (column.Attribute(Namespaces.Table + "style-name")?.Value is { } breakStyle && context.ColumnBreakStyles.Contains(breakStyle))
                foreach (var index in Breaks(columnIndex, repeat, CellAddress.MaxColumns)) sheet.ColumnBreaks.Add(index);
            if (Collapsed(column))
                sheet.Layout.HideColumns(columnIndex, LastOf(columnIndex, repeat, CellAddress.MaxColumns));
            columnIndex = rows.Advance(columnIndex, repeat);
        }

        foreach (var row in rows.In(table))
        {
            if (row.Element.Attribute(Namespaces.Table + "style-name")?.Value is { } style && context.RowHeights.TryGetValue(style, out var height)
                && (height != sheet.Layout.DefaultRowHeight || context.OptimalRows.Contains(style) == sheet.Layout.DefaultRowHeightManual))
            {
                if (context.OptimalRows.Contains(style)) sheet.Layout.SetAutomaticRowHeights(row.Index, LastOf(row.Index, row.Repeat, CellAddress.MaxRows), height);
                else sheet.Layout.SetRowHeightRange(row.Index, LastOf(row.Index, row.Repeat, CellAddress.MaxRows), height);
            }
            if (row.Element.Attribute(Namespaces.Table + "style-name")?.Value is { } breakStyle && context.RowBreakStyles.Contains(breakStyle))
                foreach (var index in Breaks(row.Index, row.Repeat, CellAddress.MaxRows)) sheet.RowBreaks.Add(index);
            if (Collapsed(row.Element))
                sheet.Layout.HideRows(row.Index, LastOf(row.Index, row.Repeat, CellAddress.MaxRows));
        }

        foreach (var group in layout.ReadRowGroups(table)) sheet.RowGroups.Add(group);
        foreach (var group in layout.ReadColumnGroups(table)) sheet.ColumnGroups.Add(group);
        sheet.OutlineSummary = new OdfOutlineSummaryCodec().Read(table);
    }

    private Twip? Prevailing(IEnumerable<(Twip? Size, int Repeat)> spans)
    {
        var totals = new Dictionary<Twip, long>();
        long unsized = 0;
        foreach (var (size, repeat) in spans)
        {
            if (size is { } known) totals[known] = totals.GetValueOrDefault(known) + repeat;
            else unsized += repeat;
        }
        if (totals.Count == 0) return null;
        var best = totals.MaxBy(pair => pair.Value);
        return best.Value > unsized ? best.Key : null;
    }
}

internal sealed class OdsCellReader(OdfStyleNames styleNames, IOdfParagraphText paragraphs)
{
    private readonly ConstantFormulas _constants = new();
    private readonly OdfDynamicArrays _dynamicArrays = new();

    public int StyleIdOf(string styleName, OdsReadContext context)
    {
        if (context.StyleIds.TryGetValue(styleName, out var mapped)) return mapped;
        return NamedStyleOf(styleName, context) is { } style ? context.Workbook.Styles.Intern(context.Workbook.NamedStyles.StyleOf(style)) : 0;
    }

    public NamedCellStyle? NamedStyleOf(string styleName, OdsReadContext context)
    {
        var named = context.Workbook.NamedStyles;
        NamedCellStyle? Common(string name) =>
            (context.NamedStyleNames.TryGetValue(name, out var display) ? named.Find(display) : named.Find(name)) is { } found
            && !string.Equals(found.Name, "Default", StringComparison.Ordinal) ? found : null;
        if (Common(styleName) is { } direct) return direct;
        if (context.Parents.TryGetValue(styleName, out var parent) && Common(parent) is { } inherited) return inherited;
        return styleNames.NamedStyleOf(styleName, named.All.Select(entry => entry.Name)) is { } marked ? named.Find(marked) : null;
    }

    public Cell? Read(XElement element, OdsReadContext context, string? columnDefault)
    {
        var named = context.Workbook.NamedStyles;
        var valueType = element.Attribute(Namespaces.Office + "value-type")?.Value;
        var formula = element.Attribute(Namespaces.Table + "formula")?.Value;
        var styleName = element.Attribute(Namespaces.Table + "style-name")?.Value ?? columnDefault;

        var namedStyle = styleName is null ? null : NamedStyleOf(styleName, context);

        var inheritedId = styleName is null ? 0 : StyleIdOf(styleName, context);
        var styleId = formula is null ? WithImpliedFormat(context.Workbook, inheritedId, valueType, element) : inheritedId;

        var hyperlink = element.Descendants(Namespaces.Text + "a")
            .Select(anchor => anchor.Attribute(Namespaces.Xlink + "href")?.Value)
            .FirstOrDefault(href => href is { Length: > 0 }) is { } href ? _links.FromOdf(href)
            : element.Attribute(Namespaces.Table + "style-name")?.Value is { } ownStyleName && context.StyleLinks.TryGetValue(ownStyleName, out var styleHref) ? _links.FromOdf(styleHref) : null;

        var ownStyle = element.Attribute(Namespaces.Table + "style-name") is not null;

        if (valueType is null && formula is null && hyperlink is null && !ownStyle) return null;

        var value = IsErrorResult(element, formula)
            ? Error(paragraphs.Read(element))
            : Value(element, valueType, context.Diagnostics);
        var internalFormula = formula is null ? null : new OdfFormulaSyntax().ToInternal(formula);

        return new Cell
        {
            Value = value,
            StyleId = styleId,
            ExplicitStyle = ownStyle,
            StyleName = namedStyle?.Name,
            Formula = _constants.IsConstant(value, internalFormula) ? null : internalFormula,
            Hyperlink = hyperlink,
            ImplicitIntersection = internalFormula is not null && !_dynamicArrays.IsMarked(element),
        };
    }

    private CellValue Value(XElement element, string? valueType, List<Diagnostic> diagnostics) => valueType switch
    {
        "float" or "percentage" or "currency" => Number(element, "value"),
        "boolean" => CellValue.Boolean(OdfBooleanValue.Parse(element.Attribute(Namespaces.Office + "boolean-value")?.Value)),
        "date" => Date(element, diagnostics),
        "time" => Time(element),
        "string" => CellValue.Text((string?)element.Attribute(Namespaces.Office + "string-value") ?? paragraphs.Read(element)),
        _ => CellValue.Empty,
    };

    private readonly OdfLinkSyntax _links = new();

    private int WithImpliedFormat(Workbook workbook, int styleId, string? valueType, XElement element)
    {
        var implied = valueType switch
        {
            "date" => element.Attribute(Namespaces.Office + "date-value")?.Value?.Contains('T') == true ? 22 : 14,
            "time" => 21,
            "percentage" => 10,
            _ => 0,
        };
        if (implied == 0) return styleId;
        var style = workbook.Styles.Get(styleId);
        return style.NumberFormatId != 0 ? styleId : workbook.Styles.Intern(style with { NumberFormatId = implied });
    }

    private readonly TokenMap<CellError> _errors = new(CellError.Value, "#VALUE!",
        (CellError.Null, "#NULL!"), (CellError.DivideByZero, "#DIV/0!"), (CellError.Value, "#VALUE!"),
        (CellError.Reference, "#REF!"), (CellError.Name, "#NAME?"), (CellError.Number, "#NUM!"),
        (CellError.NotAvailable, "#N/A"), (CellError.Spill, "#SPILL!"), (CellError.Calc, "#CALC!"),
        (CellError.Null, "#NULLO!"), (CellError.Value, "#VALORE!"), (CellError.Reference, "#RIF!"),
        (CellError.Name, "#NOME?"), (CellError.NotAvailable, "#N/D"),
        (CellError.Value, "#WERT!"), (CellError.Reference, "#BEZUG!"), (CellError.Number, "#ZAHL!"),
        (CellError.NotAvailable, "#NV"),
        (CellError.Null, "#¡NULO!"), (CellError.DivideByZero, "#¡DIV/0!"), (CellError.Value, "#¡VALOR!"),
        (CellError.Reference, "#¡REF!"), (CellError.Name, "#¿NOMBRE?"), (CellError.Number, "#¡NUM!"),
        (CellError.Null, "#NUL!"), (CellError.Value, "#VALEUR!"), (CellError.Name, "#NOM?"),
        (CellError.Number, "#NOMBRE!"));

    private bool IsErrorResult(XElement element, string? formula)
    {
        if (element.Attribute(Namespaces.Calcext + "value-type")?.Value is { } extended) return extended == "error";
        if (formula is null) return false;
        var shown = paragraphs.Read(element).Trim();
        return shown.StartsWith("Err:", StringComparison.Ordinal) || _errors.Find(shown) is not null;
    }

    private readonly Dictionary<string, CellError> _shownErrorCodes = new(StringComparer.Ordinal)
    {
        ["Err:502"] = CellError.Number, ["Err:503"] = CellError.Number, ["Err:523"] = CellError.Number,
        ["Err:522"] = CellError.Circular, ["Err:524"] = CellError.Reference, ["Err:525"] = CellError.Name,
        ["Err:532"] = CellError.DivideByZero,
    };

    private CellValue Error(string text)
    {
        var shown = text.Trim();
        return CellValue.FromError(_shownErrorCodes.TryGetValue(shown, out var code) ? code : _errors.Value(shown));
    }

    private readonly System.Text.RegularExpressions.Regex _clock = new(
        @"^(?<sign>-)?PT(?<h>[0-9]+)H(?:(?<m>[0-9]+)M)?(?:(?<s>[0-9]+(?:\.[0-9]+)?)S)?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private CellValue Time(XElement element)
    {
        var text = element.Attribute(Namespaces.Office + "time-value")?.Value;
        if (string.IsNullOrEmpty(text)) return CellValue.Empty;

        if (_clock.Match(text) is { Success: true } clock)
        {
            var seconds = double.Parse(clock.Groups["h"].Value, CultureInfo.InvariantCulture) * 3600
                + (clock.Groups["m"].Success ? double.Parse(clock.Groups["m"].Value, CultureInfo.InvariantCulture) * 60 : 0)
                + (clock.Groups["s"].Success ? double.Parse(clock.Groups["s"].Value, CultureInfo.InvariantCulture) : 0);
            return CellValue.Number((clock.Groups["sign"].Success ? -seconds : seconds) / 86400);
        }

        try
        {
            return CellValue.Number(System.Xml.XmlConvert.ToTimeSpan(text).TotalDays);
        }
        catch (FormatException)
        {
            return CellValue.Empty;
        }
    }

    private CellValue Number(XElement element, string attribute) =>
        double.TryParse(element.Attribute(Namespaces.Office + attribute)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? CellValue.Number(number)
            : CellValue.Empty;

    private CellValue Date(XElement element, List<Diagnostic> diagnostics)
    {
        var text = element.Attribute(Namespaces.Office + "date-value")?.Value;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            diagnostics.Add(Diagnostic.Warning("ODF012", $"Unreadable date: '{text}'.").With(text));
            return CellValue.Empty;
        }
        return CellValue.Number((date - new DateTime(1899, 12, 30)).TotalDays);
    }
}

internal sealed record OdsTableState(Worksheet Sheet, IReadOnlyDictionary<int, string> Defaults);

internal sealed class OdsTableReader(TableRows rows, TableCells cells, OdsCellReader cellReader)
{
    public OdsTableState Begin(XElement table, OdsReadContext context)
    {
        var workbook = context.Workbook;
        var name = table.Attribute(Namespaces.Table + "name")?.Value is { Length: > 0 } declared ? declared : workbook.UniqueSheetName(Workbook.CanonicalSheetPrefix);
        if (workbook.FindSheet(name) is not null)
        {
            context.Diagnostics.Add(Diagnostic.Warning("ODF011", $"Duplicate sheet '{name}': renamed.").With(name));
            name = workbook.UniqueSheetName(name + "_");
        }

        var sheet = workbook.AddSheet(name);
        var defaults = ColumnDefaults(table);

        foreach (var (column, columnStyle) in defaults.Where(pair => pair.Key < CellAddress.MaxColumns))
            sheet.Layout.SetColumnStyle(column, cellReader.StyleIdOf(columnStyle, context));
        return new OdsTableState(sheet, defaults);
    }

    public void ReadRow(OdsTableState state, TableRow row, OdsReadContext context)
    {
        var sheet = state.Sheet;
        var rowDefault = RowDefault(row.Element);
        var lastRow = (int)Math.Min((long)row.Index + row.Repeat - 1, CellAddress.MaxRows - 1);
        var rowCells = cells.Of(row).ToList();
        var lineStyle = rowDefault ?? rowCells.Select(TrailingStyle).LastOrDefault(name => name is not null);
        if (lineStyle is not null && row.Index < CellAddress.MaxRows)
            sheet.Layout.SetRowStyles(row.Index, lastRow, cellReader.StyleIdOf(lineStyle, context));

        var read = new List<(int Column, Cell Cell)>();
        foreach (var cell in rowCells)
        {
            if (rowDefault is null && TrailingStyle(cell) is not null) continue;

            var copies = Math.Min(cell.Repeat, CellAddress.MaxColumns);
            if (rowDefault is null && copies > 1 && cell.Address.Column < CellAddress.MaxColumns && cell.Element.Attribute(Namespaces.Table + "style-name") is null
                && Enumerable.Range(cell.Address.Column, copies).Select(column => state.Defaults.GetValueOrDefault(column)).Distinct().Skip(1).Any())
            {
                var byDefault = new Dictionary<string, Cell?>(StringComparer.Ordinal);
                for (var copy = 0; copy < copies; copy++)
                {
                    var columnDefault = state.Defaults.GetValueOrDefault(cell.Address.Column + copy);
                    var key = columnDefault ?? string.Empty;
                    if (!byDefault.TryGetValue(key, out var styled))
                        byDefault[key] = styled = cellReader.Read(cell.Element, context, columnDefault);
                    if (styled is not null && (copy < FormattingRepeat || Carries(styled)) && !Vanishes(styled, sheet.Layout, row, cell)) read.Add((cell.Address.Column + copy, styled));
                }
                continue;
            }

            if (cellReader.Read(cell.Element, context, rowDefault ?? state.Defaults.GetValueOrDefault(cell.Address.Column)) is not { } value) continue;
            if ((lineStyle is not null || row.Repeat > FormattingRepeat) && Banded(value, sheet.Layout, row, cell, lastRow)) continue;
            if (Vanishes(value, sheet.Layout, row, cell)) continue;
            var kept = Carries(value) ? copies : Math.Min(copies, FormattingRepeat);
            for (var copy = 0; copy < kept; copy++)
                read.Add((cell.Address.Column + copy, value));
        }

        if (read.Count == 0) return;
        var inside = context.Budget.Rows(row.Index, row.Repeat);
        var outside = inside < row.Repeat;
        var content = read.Where(entry => Carries(entry.Cell)).ToList();
        var granted = context.Budget.TakeCellRows(inside, content.Count);
        var formatted = Math.Min(inside, FormattingRepeat);
        for (var copy = 0; copy < Math.Max(granted, formatted); copy++)
            foreach (var (column, cell) in copy < formatted ? read : content)
            {
                if (copy >= granted && Carries(cell)) continue;
                var address = new CellAddress(row.Index + copy, column);
                if (address.IsValid) sheet[address] = cell;
                else outside = true;
            }
        if (outside && context.Diagnostics.All(diagnostic => diagnostic.Code != "ODF043"))
            context.Diagnostics.Add(Diagnostic.Warning("ODF043",
                $"The sheet '{sheet.Name}' has content beyond row {CellAddress.MaxRows} or column {CellAddress.ColumnName(CellAddress.MaxColumns - 1)}: it was not loaded.").With(sheet.Name));
    }

    private const int FormattingRepeat = 4096;

    private bool Carries(Cell cell) => !cell.Value.IsEmpty || cell.HasFormula || cell.Hyperlink is not null;

    private bool Banded(Cell value, SheetLayout layout, TableRow row, TableCell cell, int lastRow)
    {
        if (value with { StyleId = StyleCatalog.DefaultStyleId, ExplicitStyle = false } != Cell.Blank
            || row.Index >= CellAddress.MaxRows || cell.Address.Column >= CellAddress.MaxColumns)
            return false;

        var lastColumn = (int)Math.Min((long)cell.Address.Column + cell.Repeat - 1, CellAddress.MaxColumns - 1);
        var area = new CellRange(new CellAddress(row.Index, cell.Address.Column), new CellAddress(lastRow, lastColumn));
        if (layout.Styles.LineStyleAt(area.Start) != value.StyleId) layout.SetStyleBlock(area, value.StyleId);
        return true;
    }

    private bool Vanishes(Cell value, SheetLayout layout, TableRow row, TableCell cell)
    {
        if (!value.Value.IsEmpty || value.HasFormula || value.IsPartOfArrayFormula || value.Hyperlink is not null
            || value.StyleId != StyleCatalog.DefaultStyleId)
            return false;
        if (!value.ExplicitStyle) return true;
        var lastRow = (int)Math.Min((long)row.Index + row.Repeat - 1, CellAddress.MaxRows - 1);
        var lastColumn = (int)Math.Min((long)cell.Address.Column + cell.Repeat - 1, CellAddress.MaxColumns - 1);
        return !Styled(layout.RowStyles, row.Index, lastRow) && !Styled(layout.ColumnStyles, cell.Address.Column, lastColumn);
    }

    private bool Styled(IReadOnlyDictionary<int, int> styles, int first, int last)
    {
        if (styles.Count <= last - first)
            return styles.Keys.Any(index => index >= first && index <= last);
        for (var index = first; index <= last; index++)
            if (styles.ContainsKey(index)) return true;
        return false;
    }

    private const int TrailingRun = 1024;

    private string? TrailingStyle(TableCell cell) =>
        cell.Repeat >= TrailingRun
        && cell.Element.Attribute(Namespaces.Office + "value-type") is null
        && cell.Element.Attribute(Namespaces.Table + "formula") is null
        && cell.Element.Attribute(Namespaces.Table + "style-name")?.Value is { Length: > 0 } name
        && name != "Default"
            ? name
            : null;

    private string? RowDefault(XElement row) =>
        row.Attribute(Namespaces.Table + "default-cell-style-name")?.Value is { Length: > 0 } name && name != "Default" ? name : null;

    private Dictionary<int, string> ColumnDefaults(XElement table)
    {
        var defaults = new Dictionary<int, string>();
        var index = 0;
        foreach (var column in rows.Columns(table))
        {
            var repeat = rows.Repeat(column, Namespaces.Table + "number-columns-repeated");
            if (column.Attribute(Namespaces.Table + "default-cell-style-name")?.Value is { Length: > 0 } name && name != "Default")
                for (var offset = 0; offset < repeat && index + offset < CellAddress.MaxColumns; offset++)
                    defaults[index + offset] = name;
            index = rows.Advance(index, repeat);
        }
        return defaults;
    }
}

internal sealed class OdsNamedExpressionReader
{
    public void Read(XDocument content, Workbook workbook)
    {
        foreach (var container in content.Descendants(Namespaces.Table + "named-expressions"))
        {
            var table = container.Parent?.Name == Namespaces.Table + "table" ? container.Parent : null;
            if (table?.Element(Namespaces.Table + "scenario") is not null) continue;
            var scope = table?.Attribute(Namespaces.Table + "name")?.Value;
            Read(container, workbook, scope);
        }
    }

    private void Read(XElement container, Workbook workbook, string? scope)
    {
        foreach (var element in container.Elements())
        {
            if (element.Attribute(Namespaces.Table + "name")?.Value is not { Length: > 0 } name) continue;
            if (name.StartsWith("_xlnm.", StringComparison.Ordinal)) continue;

            if (element.Name == Namespaces.Table + "named-range")
            {
                if (element.Attribute(Namespaces.Table + "cell-range-address")?.Value is { Length: > 0 } address)
                    workbook.DefineName(new DefinedName(name, FromBase(Internal(address), element), scope));
                continue;
            }

            if ((element.Attribute(Namespaces.Table + "expression") ?? element.Attribute(Namespaces.Of + "expression"))?.Value is { Length: > 0 } expression)
                workbook.DefineName(new DefinedName(name, FromBase((new OdfFormulaSyntax().ToInternal(expression) ?? expression).TrimStart('='), element), scope));
        }
    }

    private string FromBase(string expression, XElement element)
    {
        if (element.Attribute(Namespaces.Table + "base-cell-address")?.Value is not { Length: > 0 } written) return expression;
        var baseCell = Internal(written);
        var separator = baseCell.LastIndexOf('.');
        if (!PulseOffice.Sheets.Addressing.CellAddress.TryParseA1(baseCell[(separator + 1)..].Replace("$", string.Empty, StringComparison.Ordinal), out var origin)
            || origin == PulseOffice.Sheets.Addressing.CellAddress.First) return expression;
        return new PulseOffice.Sheets.Engine.Operations.WrappingOffsetRewriter(-origin.Row, -origin.Column).Rewrite(expression)?.TrimStart('=') ?? expression;
    }

    private string Internal(string address) =>
        new OdfFormulaSyntax().ToInternalExpression("[" + address + "]");
}
