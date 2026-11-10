using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsCellWriter(OdfStyleMapper styles, IOdfParagraphText paragraphs)
{
    public string? DefaultStyleName(int styleId, Workbook workbook)
    {
        var style = workbook.Styles.Get(styleId);
        var code = style.NumberFormatId == 0 ? null : workbook.NumberFormats.CodeOf(style.NumberFormatId);
        return styles.NameFor(styleId, style, null, code);
    }

    public XElement Write(Cell cell, Workbook workbook)
    {
        var element = new XElement(Namespaces.Table + "table-cell");

        var parent = cell.StyleName is { Length: > 0 } named && workbook.NamedStyles.Find(named) is not null ? named : null;
        var cellStyle = workbook.Styles.Get(cell.StyleId);
        var inferred = cell.HasFormula && cellStyle.NumberFormatId == 0 ? cell.InferredFormatId : 0;
        if (inferred != 0) cellStyle = cellStyle with { NumberFormatId = inferred };
        var code = cellStyle.NumberFormatId == 0 ? null : workbook.NumberFormats.CodeOf(cellStyle.NumberFormatId);
        var styleLink = cell.Hyperlink is { Length: > 0 } target && cell.Value.Kind != ValueKind.Text ? _links.ToOdf(target) : null;
        var parentStyle = parent is null ? null : workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find(parent)!);
        if (cellStyle.Language is null && parentStyle?.Language is { Length: > 0 })
            cellStyle = cellStyle with { Language = workbook.TextLanguage ?? LanguageProperty.NoLanguage };
        if (styles.NameFor(cell.StyleId, cellStyle, parent, code, styleLink, inferred, parentStyle) is { } styleName)
            element.Add(new XAttribute(Namespaces.Table + "style-name", styleName));

        if (cell.HasFormula) element.Add(new XAttribute(Namespaces.Table + "formula", new OdfFormulaSyntax().ToOdf(cell.Formula!)));
        if (cell.IsArrayAnchor && cell.ArrayFormulaRange is { } array)
            element.Add(
                new XAttribute(Namespaces.Table + "number-matrix-columns-spanned", array.ColumnCount.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Table + "number-matrix-rows-spanned", array.RowCount.ToString(CultureInfo.InvariantCulture)));
        else if (cell.HasFormula && !cell.ImplicitIntersection && (cell.SpillRange is not null || cell.UsesArrays))
            element.Add(
                new XAttribute(Namespaces.Table + "number-matrix-columns-spanned", (cell.SpillRange?.ColumnCount ?? 1).ToString(CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Table + "number-matrix-rows-spanned", (cell.SpillRange?.RowCount ?? 1).ToString(CultureInfo.InvariantCulture)),
                _dynamicArrays.Mark());

        var value = cell.Value;
        switch (value.Kind)
        {
            case ValueKind.Number:
                _numbers.Write(element, value.AsNumber(), code);
                element.Add(Paragraph(value.ToString()));
                break;
            case ValueKind.Boolean:
                element.Add(Type("boolean"), new XAttribute(Namespaces.Office + "boolean-value", value.AsBoolean() ? "true" : "false"), Paragraph(value.ToString()));
                break;
            case ValueKind.Text:
                element.Add(Type("string"), paragraphs.Paragraphs(value.AsText()));
                break;
            case ValueKind.Error:
                if (!cell.HasFormula && !cell.IsPartOfArrayFormula)
                    element.Add(new XAttribute(Namespaces.Table + "formula", new OdfFormulaSyntax().ToOdf("=" + value.ErrorText)));
                element.Add(Type("float"), new XAttribute(Namespaces.Office + "value", "0"),
                    new XAttribute(Namespaces.Calcext + "value-type", "error"), Paragraph(value.ErrorText));
                break;
        }
        if (cell.Hyperlink is { Length: > 0 } link) Link(element, link);
        return element;
    }

    private void Link(XElement cell, string link)
    {
        var paragraph = cell.Element(Namespaces.Text + "p");
        if (paragraph is null)
        {
            paragraph = new XElement(Namespaces.Text + "p");
            cell.Add(paragraph);
        }
        var anchor = new XElement(Namespaces.Text + "a",
            new XAttribute(Namespaces.Xlink + "href", _links.ToOdf(link)), new XAttribute(Namespaces.Xlink + "type", "simple"),
            paragraph.Nodes().ToList());
        paragraph.RemoveNodes();
        paragraph.Add(anchor);
    }

    private readonly OdfLinkSyntax _links = new();
    private readonly OdfDynamicArrays _dynamicArrays = new();
    private readonly OdsNumberValueWriter _numbers = new();

    private XAttribute Type(string type) => new(Namespaces.Office + "value-type", type);

    private XElement Paragraph(string text) => new(Namespaces.Text + "p", text);
}

internal sealed class OdsAnnotationBuilder(IOdfParagraphText paragraphs)
{
    public XElement Build(CellComment comment)
    {
        var annotation = new XElement(Namespaces.Office + "annotation",
            new XAttribute(Namespaces.Office + "display", comment.IsAlwaysVisible ? "true" : "false"));
        if (comment.Author is { Length: > 0 } author) annotation.Add(new XElement(Namespaces.Dc + "creator", author));
        annotation.Add(new XElement(Namespaces.Dc + "date", comment.CreatedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
        annotation.Add(paragraphs.Paragraphs(comment.Text));
        return annotation;
    }
}

internal sealed class OutlineNesting
{
    public IEnumerable<XElement> Nest(IReadOnlyList<XElement> elements, IReadOnlyList<OutlineGroup> groups, string groupName)
    {
        if (groups.Count == 0 || elements.Count == 0) return elements;

        var result = new List<XElement>(elements);
        foreach (var group in groups.OrderByDescending(group => group.First).ThenByDescending(group => group.Level))
        {
            if (group.First >= result.Count) continue;

            var last = Math.Min(group.Last, result.Count - 1);
            var taken = result.GetRange(group.First, last - group.First + 1);
            var wrapper = new XElement(Namespaces.Table + groupName, taken);
            if (group.IsCollapsed) wrapper.Add(new XAttribute(Namespaces.Table + "display", "false"));

            result.RemoveRange(group.First, taken.Count);
            result.Insert(group.First, wrapper);
        }
        return result;
    }
}

internal sealed record OdsRowSpan(int First, int Last, XElement Element);

internal sealed class OdsRowGrouping
{
    public void Write(XmlWriter writer, IEnumerable<OdsRowSpan> rows, IReadOnlyList<OutlineGroup> groups, int header, Action<XElement> emit)
    {
        var headed = header > 0 && !groups.Any(group => group.First < header);
        var inHeader = false;
        var open = new List<OutlineGroup>();
        var pending = groups.OrderBy(group => group.First).ThenByDescending(group => group.Last).ThenBy(group => group.Level).ToList();
        var next = 0;
        var waiting = new List<OutlineGroup>();

        foreach (var row in rows)
        {
            if (headed)
            {
                if (row.Last < header)
                {
                    if (!inHeader) writer.WriteStartElement("table-header-rows", Namespaces.Table.NamespaceName);
                    inHeader = true;
                    emit(row.Element);
                    continue;
                }
                if (inHeader) writer.WriteEndElement();
                headed = inHeader = false;
            }

            while (open.Count > 0 && open[^1].Last < row.First)
            {
                writer.WriteEndElement();
                open.RemoveAt(open.Count - 1);
            }

            while (next < pending.Count && pending[next].First <= row.First) waiting.Add(pending[next++]);
            waiting.RemoveAll(group => group.Last < row.First);

            for (var index = 0; index < waiting.Count;)
            {
                var group = waiting[index];
                if (open.Count > 0 && (group.Last > open[^1].Last || Same(group, open[^1])))
                {
                    if (Same(group, open[^1])) waiting.RemoveAt(index);
                    else index++;
                    continue;
                }

                writer.WriteStartElement("table-row-group", Namespaces.Table.NamespaceName);
                if (group.IsCollapsed) writer.WriteAttributeString("display", Namespaces.Table.NamespaceName, "false");
                open.Add(group);
                waiting.RemoveAt(index);
                index = 0;
            }

            emit(row.Element);
        }

        if (inHeader) writer.WriteEndElement();
        foreach (var _ in open) writer.WriteEndElement();
    }

    private bool Same(OutlineGroup group, OutlineGroup other) =>
        group.First == other.First && group.Last == other.Last && group.Level == other.Level;
}

internal interface IFrameSource
{
    IReadOnlyDictionary<CellAddress, List<XElement>> Frames(Worksheet sheet);
}

internal sealed class FrameSource(Func<Worksheet, IReadOnlyDictionary<CellAddress, List<XElement>>> frames) : IFrameSource
{
    public IReadOnlyDictionary<CellAddress, List<XElement>> Frames(Worksheet sheet) => frames(sheet);
}

internal sealed class AnchoredFrameSet(IReadOnlyList<IFrameSource> sources)
{
    public Dictionary<CellAddress, List<XElement>> Collect(Worksheet sheet)
    {
        var merged = new Dictionary<CellAddress, List<XElement>>();
        foreach (var source in sources)
            foreach (var (address, frames) in source.Frames(sheet))
            {
                if (!merged.TryGetValue(address, out var list)) merged[address] = list = [];
                list.AddRange(frames);
            }
        return merged;
    }
}

internal sealed class OdsNamedExpressionWriter
{
    private readonly System.Text.RegularExpressions.Regex _reference = new(
        @"^(?:(?:'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_]*)[.!])?\$?[A-Za-z]{1,3}\$?[0-9]{1,7}(?::\$?[A-Za-z]{1,3}\$?[0-9]{1,7})?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly OdfFormulaSyntax _syntax = new();

    public XElement? Build(Workbook workbook) => Build(workbook, null);

    public XElement? Build(Workbook workbook, string? scope)
    {
        var names = workbook.DefinedNames
            .Where(name => string.Equals(name.Scope, scope, StringComparison.OrdinalIgnoreCase)
                || scope is null && name.Scope is not null && workbook.FindSheet(name.Scope) is null)
            .ToList();
        if (names.Count == 0) return null;

        var baseSheet = scope ?? (workbook.Sheets.Count > 0 ? workbook.Sheets[0].Name : "Sheet1");

        return new XElement(Namespaces.Table + "named-expressions", names.Select(name =>
        {
            var expression = name.Expression.Trim().TrimStart('=');

            return _reference.IsMatch(expression)
                ? new XElement(Namespaces.Table + "named-range",
                    new XAttribute(Namespaces.Table + "name", name.Name),
                    new XAttribute(Namespaces.Table + "base-cell-address", Address(baseSheet, "$A$1")),
                    new XAttribute(Namespaces.Table + "cell-range-address", Address(baseSheet, expression)))
                : new XElement(Namespaces.Table + "named-expression",
                    new XAttribute(Namespaces.Table + "name", name.Name),
                    new XAttribute(Namespaces.Table + "base-cell-address", Address(baseSheet, "$A$1")),
                    new XAttribute(Namespaces.Table + "expression", _syntax.ToOdf("=" + expression)));
        }));
    }

    private string Address(string baseSheet, string expression)
    {
        var odf = _syntax.ToOdfExpression("=" + expression).Trim('[', ']');
        if (!odf.StartsWith('.')) return "$" + odf;

        var qualified = _syntax.ToOdfExpression("='" + baseSheet.Replace("'", "''", StringComparison.Ordinal) + "'." + expression).Trim('[', ']');
        return "$" + qualified;
    }
}

internal sealed class OdsStylesDocumentBuilder
{
    private const string RootStyle = "Default";

    private string? CodeOf(Workbook workbook, int id) => id == 0 ? null : workbook.NumberFormats.CodeOf(id);

    public XDocument Build(IReadOnlyList<XElement> conditionalStyles, IReadOnlyList<XElement> shapeDefinitions, Workbook workbook)
    {
        var declarer = new OdfStyleMapper("NS");
        var root = workbook.NamedStyles.Find(RootStyle) is { } declaredRoot
            ? declarer.DeclareNamed(RootStyle, workbook.Styles.Get(declaredRoot.StyleId), null, CodeOf(workbook, workbook.Styles.Get(declaredRoot.StyleId).NumberFormatId), declaredRoot.Explicit)
            : new XElement(Namespaces.Style + "style", new XAttribute(Namespaces.Style + "name", RootStyle), new XAttribute(Namespaces.Style + "family", "table-cell"));
        var named = workbook.NamedStyles.All
            .Where(style => !string.Equals(style.Name, RootStyle, StringComparison.OrdinalIgnoreCase))
            .Select(style => declarer.DeclareNamed(style.Name, workbook.Styles.Get(style.StyleId), style.Parent,
                CodeOf(workbook, workbook.Styles.Get(style.StyleId).NumberFormatId), style.Explicit))
            .ToList();
        var pages = new OdfPageStyleMapper().Build(workbook.PageStyles);

        return new XDocument(new XElement(Namespaces.Office + "document-styles",
            new XAttribute(XNamespace.Xmlns + "office", Namespaces.Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "style", Namespaces.Style.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "fo", Namespaces.Fo.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "draw", Namespaces.Draw.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "svg", Namespaces.Svg.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "loext", Namespaces.Loext.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "number", Namespaces.Number.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "text", Namespaces.Text.NamespaceName),
            new XAttribute(Namespaces.Office + "version", "1.3"),
            new XElement(Namespaces.Office + "styles", declarer.Declarations,
                new OdsDefaultCellStyle().Build(workbook),
                root,
                named, conditionalStyles, shapeDefinitions),
            new XElement(Namespaces.Office + "automatic-styles", pages.Layouts),
            new XElement(Namespaces.Office + "master-styles", pages.Masters)));
    }
}

internal sealed class OdsNumberValueWriter
{
    private static readonly DateTime Epoch = new(1899, 12, 30);
    private readonly OdfNumberStyleWriter _styles = new();

    public void Write(XElement cell, double number, string? code)
    {
        var type = code is null ? OdfValueType.Float : _styles.TypeOf(code);
        switch (type)
        {
            case OdfValueType.Date when number is >= -657434 and <= 2958465:
                cell.Add(Type("date"), new XAttribute(Namespaces.Office + "date-value",
                    DateText(number)));
                break;
            case OdfValueType.Time when number >= 0 && number < 100000:
                cell.Add(Type("time"), new XAttribute(Namespaces.Office + "time-value",
                    Duration(number * 86400)));
                break;
            case OdfValueType.Percentage:
                cell.Add(Type("percentage"), Value(number));
                break;
            case OdfValueType.Currency:
                cell.Add(Type("currency"), Value(number));
                break;
            default:
                cell.Add(Type("float"), Value(number));
                break;
        }
    }

    private XAttribute Type(string type) => new(Namespaces.Office + "value-type", type);

    private string DateText(double number) =>
        number % 1 == 0
            ? Epoch.AddDays(number).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Epoch.AddTicks((long)Math.Round(number * TimeSpan.TicksPerDay)).ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);

    private string Duration(double totalSeconds)
    {
        var whole = (long)Math.Floor(totalSeconds);
        var seconds = Math.Round(totalSeconds - whole + whole % 60, 9);
        return string.Create(CultureInfo.InvariantCulture, $"PT{whole / 3600:00}H{whole / 60 % 60:00}M{seconds:00.#########}S");
    }

    private XAttribute Value(double number) => new(Namespaces.Office + "value", number.ToString("R", CultureInfo.InvariantCulture));
}

internal sealed class OdsRowIndex
{
    private readonly CellRowIndex _points;
    private readonly int[] _segmentStarts;
    private readonly int[]?[] _segmentEdges;

    public OdsRowIndex(Worksheet sheet, IEnumerable<CellAddress> anchors)
    {
        _points = new CellRowIndex(sheet.CellAddresses.Concat(anchors)
            .Concat(sheet.Merges.All.Select(region => region.Start))
            .Concat(sheet.Comments.Keys));
        List<CellRange> bands = [.. sheet.Merges.All, .. sheet.Validations.Select(validation => validation.Range)];
        (_segmentStarts, _segmentEdges) = Segments(bands);
        LastRow = Math.Max(_points.LastRow, bands.Count == 0 ? -1 : bands.Max(band => band.End.Row));
        ColumnCount = Math.Max(_points.ColumnCount, bands.Count == 0 ? 0 : bands.Max(band => band.End.Column + 1));
    }

    private (int[] Starts, int[]?[] Edges) Segments(List<CellRange> bands)
    {
        var boundaries = bands.SelectMany(band => new[] { band.Start.Row, band.End.Row + 1 }).Distinct().Order().ToArray();
        var edges = new int[]?[boundaries.Length];
        var opening = bands.OrderBy(band => band.Start.Row).ToList();
        var active = new List<CellRange>();
        var next = 0;
        for (var segment = 0; segment < boundaries.Length; segment++)
        {
            var row = boundaries[segment];
            active.RemoveAll(band => band.End.Row < row);
            while (next < opening.Count && opening[next].Start.Row <= row) active.Add(opening[next++]);
            edges[segment] = active.Count == 0 ? null
                : [.. active.SelectMany(band => new[] { band.Start.Column, band.End.Column }).Distinct().Order()];
        }
        return (boundaries, edges);
    }

    public int LastRow { get; }

    public int ColumnCount { get; }

    public bool Contains(int row) => _points.Contains(row) || BandEdges(row) is not null;

    public bool HasPoints(int row) => _points.Contains(row);

    public int NextBandBoundary(int row)
    {
        var position = Array.BinarySearch(_segmentStarts, row + 1);
        if (position < 0) position = ~position;
        return position < _segmentStarts.Length ? _segmentStarts[position] : int.MaxValue;
    }

    public IReadOnlyList<int>? Columns(int row)
    {
        var points = _points.Columns(row);
        if (BandEdges(row) is not { } edges) return points;
        if (points is null) return edges;

        var columns = new SortedSet<int>(points);
        columns.UnionWith(edges);
        return [.. columns];
    }

    private int[]? BandEdges(int row)
    {
        var position = Array.BinarySearch(_segmentStarts, row);
        if (position < 0) position = ~position - 1;
        return position < 0 ? null : _segmentEdges[position];
    }
}
