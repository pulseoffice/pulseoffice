using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfRangeFilterCodec
{
    private const int MaximumDepth = 64;

    private readonly OdfFilterCriteria _criteria = new();
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly FilterTreeColumns _columns = new();

    private XName And => Namespaces.Table + "filter-and";

    private XName Or => Namespaces.Table + "filter-or";

    private XName Condition => Namespaces.Table + "filter-condition";

    public XElement? Filter(Workbook workbook, int sheetIndex, CellRange range, bool header, IReadOnlyList<XElement> columnConditions, RangeFilter? filter)
    {
        var parts = new List<XElement>(columnConditions);
        var criteria = filter?.Criteria is { } advanced && advanced.SheetIndex >= 0 && advanced.SheetIndex < workbook.Sheets.Count ? advanced : null;

        if (criteria is not null) parts.AddRange(Derived(workbook, sheetIndex, range, header, criteria));
        else if (filter?.Condition is { } condition) parts.AddRange(Flatten(Node(condition), And));

        if (parts.Count == 0 && filter is { UniqueOnly: true }) parts.Add(Always());
        if (parts.Count == 0) return null;

        var element = new XElement(Namespaces.Table + "filter", parts.Count == 1 ? parts[0] : new XElement(And, parts));
        if (criteria is not null)
        {
            element.Add(new XAttribute(Namespaces.Table + "condition-source-range-address", _ranges.Write(workbook.Sheets[criteria.SheetIndex].Name, criteria.Range)));
            element.Add(new XAttribute(Namespaces.Table + "condition-source", "cell-range"));
        }
        if (filter is { UniqueOnly: true }) element.Add(new XAttribute(Namespaces.Table + "display-duplicates", "false"));
        return element;
    }

    public RangeFilter? ReadAdvanced(XElement? filter, Func<string, int> sheetIndex)
    {
        if (filter is null) return null;
        var unique = (string?)filter.Attribute(Namespaces.Table + "display-duplicates") == "false";
        AdvancedCriteria? criteria = null;
        if (_ranges.Read((string?)filter.Attribute(Namespaces.Table + "condition-source-range-address")) is { } source
            && sheetIndex(source.Sheet) is var index and >= 0)
            criteria = new AdvancedCriteria(index, source.Range);
        return criteria is null && !unique ? null : new RangeFilter { Criteria = criteria, UniqueOnly = unique };
    }

    public bool IsAdvanced(XElement? filter) =>
        filter?.Attribute(Namespaces.Table + "condition-source-range-address") is not null;

    public FilterNode? ReadTree(XElement? filter, int columns)
    {
        var root = filter?.Elements().FirstOrDefault(child => child.Name == And || child.Name == Or || child.Name == Condition);
        return root is null ? null : Read(root, columns, 0);
    }

    public bool IsPlain(XElement? filter) =>
        filter is null || filter.Descendants(Condition).All(condition =>
            !condition.HasElements
            && condition.Attribute(Namespaces.Loext + "data-type") is null
            && (string?)condition.Attribute(Namespaces.Table + "operator") is not ("empty" or "!empty" or "top values" or "bottom values" or "top percent" or "bottom percent"));

    public bool FitsColumns(FilterNode? tree) => _columns.ToColumns(tree) is not null;

    private FilterNode? Read(XElement element, int columns, int depth)
    {
        if (depth > MaximumDepth) return null;
        if (element.Name == Condition)
        {
            var field = int.TryParse((string?)element.Attribute(Namespaces.Table + "field-number"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : -1;
            return field < 0 || field >= columns
                ? null
                : new FilterTerm(field, _criteria.FromOdf((string?)element.Attribute(Namespaces.Table + "operator"), (string?)element.Attribute(Namespaces.Table + "value") ?? string.Empty));
        }

        var children = element.Elements().Select(child => Read(child, columns, depth + 1)).OfType<FilterNode>().ToList();
        return children.Count switch
        {
            0 => null,
            1 => children[0],
            _ => new FilterGroup(element.Name == Or ? FilterGroupJoin.Or : FilterGroupJoin.And, children),
        };
    }

    private XElement Node(FilterNode node) => node switch
    {
        FilterTerm term => Term(term.Field, term.Criteria),
        FilterGroup group => new XElement(group.Join == FilterGroupJoin.Or ? Or : And,
            group.Children.SelectMany(child => Flatten(Node(child), group.Join == FilterGroupJoin.Or ? Or : And))),
        _ => throw new InvalidOperationException("Unknown filter node."),
    };

    private IEnumerable<XElement> Flatten(XElement element, XName join) =>
        element.Name == join ? element.Elements() : [element];

    private XElement Term(int field, string criteria)
    {
        var (op, value) = _criteria.ToOdf(criteria);
        return new XElement(Condition,
            new XAttribute(Namespaces.Table + "field-number", field),
            new XAttribute(Namespaces.Table + "value", value),
            new XAttribute(Namespaces.Table + "operator", op),
            new XAttribute(Namespaces.Table + "data-type", double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? "number" : "text"));
    }

    private XElement Always() => new(Or,
        new XElement(Condition, new XAttribute(Namespaces.Table + "field-number", 0), new XAttribute(Namespaces.Table + "value", string.Empty), new XAttribute(Namespaces.Table + "operator", "empty")),
        new XElement(Condition, new XAttribute(Namespaces.Table + "field-number", 0), new XAttribute(Namespaces.Table + "value", string.Empty), new XAttribute(Namespaces.Table + "operator", "!empty")));

    private IEnumerable<XElement> Derived(Workbook workbook, int sheetIndex, CellRange range, bool header, AdvancedCriteria criteria)
    {
        var data = workbook.Sheets[sheetIndex];
        var source = workbook.Sheets[criteria.SheetIndex];
        var fields = new Dictionary<int, int>();
        for (var column = criteria.Range.Start.Column; column <= criteria.Range.End.Column; column++)
        {
            var name = source[new CellAddress(criteria.Range.Start.Row, column)].Value.ToString();
            if (name.Length == 0) continue;
            for (var target = range.Start.Column; target <= range.End.Column; target++)
                if (string.Equals(data[new CellAddress(range.Start.Row, target)].Value.ToString(), name, StringComparison.CurrentCultureIgnoreCase))
                {
                    fields[column] = target - range.Start.Column;
                    break;
                }
        }

        var rows = new List<FilterNode>();
        for (var row = criteria.Range.Start.Row + 1; row <= criteria.Range.End.Row; row++)
        {
            var terms = fields
                .Select(pair => (Field: pair.Value, Value: source[new CellAddress(row, pair.Key)].Value))
                .Where(entry => !entry.Value.IsEmpty)
                .Select(entry => (FilterNode)new FilterTerm(entry.Field, entry.Value.ToString()))
                .ToList();
            if (terms.Count == 1) rows.Add(terms[0]);
            else if (terms.Count > 1) rows.Add(new FilterGroup(FilterGroupJoin.And, terms));
        }

        if (rows.Count == 0) return [Always()];
        var tree = rows.Count == 1 ? rows[0] : new FilterGroup(FilterGroupJoin.Or, rows);
        return Flatten(Node(tree), And);
    }
}
