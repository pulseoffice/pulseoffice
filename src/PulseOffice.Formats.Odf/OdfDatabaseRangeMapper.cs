using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed record FilterOperator(string Criteria, string Odf);

internal sealed class OdfFilterCriteria
{
    private readonly IReadOnlyList<FilterOperator> _operators =
    [
        new(">=", ">="), new("<=", "<="), new("<>", "!="), new(">", ">"), new("<", "<"), new("=", "="),
    ];

    public (string Operator, string Value) ToOdf(string criteria)
    {
        var match = _operators.FirstOrDefault(op => criteria.StartsWith(op.Criteria, StringComparison.Ordinal));
        var (op, value) = match is null ? ("=", criteria) : (match.Odf, criteria[match.Criteria.Length..]);
        if (op is not ("=" or "!=") || Literal(value)) return (op, value);
        var negated = op == "!=" ? "!" : string.Empty;
        if (value.Length > 2 && value[0] == '*' && value[^1] == '*' && Literal(value[1..^1])) return (negated + "contains", value[1..^1]);
        if (value.Length > 1 && value[^1] == '*' && Literal(value[..^1])) return (negated + "begins-with", value[..^1]);
        if (value.Length > 1 && value[0] == '*' && Literal(value[1..])) return (negated + "ends-with", value[1..]);
        return (op, value);
    }

    public string FromOdf(string? odfOperator, string value) => odfOperator switch
    {
        "contains" => "*" + value + "*",
        "!contains" => "<>*" + value + "*",
        "begins-with" => value + "*",
        "!begins-with" => "<>" + value + "*",
        "ends-with" => "*" + value,
        "!ends-with" => "<>*" + value,
        _ => _operators.FirstOrDefault(op => op.Odf == odfOperator) is { } match && match.Criteria != "=" ? match.Criteria + value : value,
    };

    private bool Literal(string value) => value.IndexOfAny(['*', '?', '~']) < 0;
}

internal sealed class OdfTopItemsCodec
{
    private readonly IReadOnlyList<(bool Bottom, bool Percent, string Odf)> _operators =
    [
        (false, false, "top values"), (true, false, "bottom values"), (false, true, "top percent"), (true, true, "bottom percent"),
    ];

    public string ToOdf(TopItemsCriterion top) => _operators.First(entry => entry.Bottom == top.Bottom && entry.Percent == top.Percent).Odf;

    public TopItemsCriterion? FromOdf(string? odfOperator, string value)
    {
        var match = _operators.FirstOrDefault(entry => entry.Odf == odfOperator);
        if (match.Odf is null) return null;
        return new TopItemsCriterion(match.Bottom, match.Percent,
            double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var count) && double.IsFinite(count) && count > 0 ? count : 10);
    }
}

internal sealed class OdfColorFilterCodec
{
    private const string BackgroundType = "background-color";
    private const string TextType = "text-color";

    public string DataType(ColorFilterTarget target) => target == ColorFilterTarget.Background ? BackgroundType : TextType;

    public string ToOdf(ColorFilterCriterion color) =>
        color.ColorHex?.ToLowerInvariant() ?? (color.Target == ColorFilterTarget.Background ? "transparent" : "window-font-color");

    public ColorFilterCriterion? FromOdf(string? dataType, string value)
    {
        var target = dataType switch
        {
            BackgroundType => ColorFilterTarget.Background,
            TextType => ColorFilterTarget.Text,
            _ => (ColorFilterTarget?)null,
        };
        if (target is not { } known) return null;
        var hex = value.StartsWith('#') && value.Length == 7 ? value.ToUpperInvariant() : null;
        return new ColorFilterCriterion(known, hex);
    }
}

internal sealed class OdfDatabaseRangeMapper
{
    private const string AnonymousPrefix = "__Anonymous_Sheet_DB__";

    private readonly OdfFilterCriteria _criteria = new();
    private readonly OdfColorFilterCodec _colors = new();
    private readonly OdfTopItemsCodec _top = new();
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly OdfQueryBindingMapper _queries = new();
    private readonly OdfRangeFilterCodec _rangeFilters = new();
    private readonly FilterTreeColumns _columns = new();
    private readonly FilterColumnDates _dates = new();
    private readonly OdfSortCodec _sorts = new();

    public XElement? Build(Workbook workbook)
    {
        var container = new XElement(Namespaces.Table + "database-ranges");
        var queries = _queries.Build(workbook).ToList();
        if (queries.Count > 0) container.Add(new XAttribute(XNamespace.Xmlns + "pulseoffice-query", _queries.Namespace.NamespaceName));
        container.Add(queries);

        foreach (var range in workbook.DatabaseRanges.All)
        {
            if (range.SheetIndex < 0 || range.SheetIndex >= workbook.Sheets.Count) continue;
            var element = Range(workbook, range.SheetIndex, range.Name, range.Range, range.HasHeader, range.ShowsFilterButtons, range.Filters, range.RangeFilter, range.Sort);
            if (range.HasTotalsRow) element.Add(new XAttribute(Namespaces.Calcext + "contains-footer", "true"));
            container.Add(element);
        }

        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            var sheet = workbook.Sheets[index];
            var stored = sheet.RangeFilter;
            if (sheet.AutoFilter.Range is { } filtered)
                container.Add(Range(workbook, index, AnonymousPrefix + index, filtered, sheet.AutoFilter.HasHeader, true, sheet.AutoFilter.Filters,
                    stored is not null && stored.Range == filtered ? stored.Filter : null, sheet.AutoFilter.Sort));
            else if (stored is not null)
                container.Add(Range(workbook, index, AnonymousPrefix + index, stored.Range, stored.HasHeader, false, [], stored.Filter,
                    sheet.RangeSort is { } shared && shared.Range == stored.Range && shared.HasHeader == stored.HasHeader ? shared.Settings : null));
            else if (sheet.RangeSort is { } plain)
                container.Add(Range(workbook, index, AnonymousPrefix + index, plain.Range, plain.HasHeader, false, [], null, plain.Settings));
        }

        return container.HasElements ? container : null;
    }

    private XElement Range(Workbook workbook, int sheetIndex, string name, CellRange range, bool header, bool buttons, IReadOnlyList<ColumnFilter> filters, RangeFilter? stored, SortSettings? sort)
    {
        var sheet = workbook.Sheets[sheetIndex].Name;
        var element = new XElement(Namespaces.Table + "database-range",
            new XAttribute(Namespaces.Table + "name", name),
            new XAttribute(Namespaces.Table + "target-range-address", _ranges.Write(sheet, range)),
            new XAttribute(Namespaces.Table + "contains-header", header ? "true" : "false"));
        if (buttons) element.Add(new XAttribute(Namespaces.Table + "display-filter-buttons", "true"));
        var conditions = filters.SelectMany(filter => Conditions(filter, filter.AllowedValues is { Count: > 0 }
            ? _dates.Read(workbook, workbook.Sheets[sheetIndex], range, filter.ColumnOffset, header)
            : new Dictionary<string, DateTime>())).ToList();
        if (_rangeFilters.Filter(workbook, sheetIndex, range, header, conditions, stored) is { } filter) element.Add(filter);
        if (_sorts.Build(sort, range) is { } sorting) element.Add(_sorts.Orientation(sort), sorting);
        return element;
    }

    private IEnumerable<XElement> Conditions(ColumnFilter filter, IReadOnlyDictionary<string, DateTime> dates)
    {
        if (filter.Color is { } color)
            yield return new XElement(Namespaces.Table + "filter-condition",
                new XAttribute(Namespaces.Table + "field-number", filter.ColumnOffset),
                new XAttribute(Namespaces.Table + "value", _colors.ToOdf(color)),
                new XAttribute(Namespaces.Table + "operator", "="),
                new XAttribute(Namespaces.Loext + "data-type", _colors.DataType(color.Target)));

        if (filter.AllowedValues is { Count: 0 } && !filter.IncludeBlanks)
        {
            yield return Emptiness(filter.ColumnOffset, "empty");
            yield return Emptiness(filter.ColumnOffset, "!empty");
        }
        else if (filter.AllowedValues is { } allowed)
            yield return new XElement(Namespaces.Table + "filter-condition",
                new XAttribute(Namespaces.Table + "field-number", filter.ColumnOffset),
                new XAttribute(Namespaces.Table + "value", allowed.FirstOrDefault() ?? string.Empty),
                new XAttribute(Namespaces.Table + "operator", "="),
                allowed.Select(value => dates.TryGetValue(value, out var moment) ? _dates.IsoText(moment) : value)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(value =>
                    new XElement(Namespaces.Table + "filter-set-item", new XAttribute(Namespaces.Table + "value", value))),
                filter.IncludeBlanks ? new XElement(Namespaces.Table + "filter-set-item", new XAttribute(Namespaces.Table + "value", string.Empty)) : null);
        else if (!filter.IncludeBlanks)
            yield return Emptiness(filter.ColumnOffset, "!empty");

        if (filter.Top is { } top)
            yield return new XElement(Namespaces.Table + "filter-condition",
                new XAttribute(Namespaces.Table + "field-number", filter.ColumnOffset),
                new XAttribute(Namespaces.Table + "value", top.Count.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Table + "operator", _top.ToOdf(top)),
                new XAttribute(Namespaces.Table + "data-type", "number"));

        var criteria = filter.Conditions.Select(condition => Condition(filter.ColumnOffset, condition)).ToList();
        if (criteria.Count > 1 && filter.Join == CriteriaJoin.Or)
            yield return new XElement(Namespaces.Table + "filter-or", criteria);
        else
            foreach (var condition in criteria) yield return condition;
    }

    private XElement Condition(int column, string criteria)
    {
        var (op, value) = _criteria.ToOdf(criteria);
        return new XElement(Namespaces.Table + "filter-condition",
            new XAttribute(Namespaces.Table + "field-number", column),
            new XAttribute(Namespaces.Table + "value", value),
            new XAttribute(Namespaces.Table + "operator", op),
            new XAttribute(Namespaces.Table + "data-type", double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? "number" : "text"));
    }

    private XElement Emptiness(int column, string op) =>
        new(Namespaces.Table + "filter-condition",
            new XAttribute(Namespaces.Table + "field-number", column),
            new XAttribute(Namespaces.Table + "value", string.Empty),
            new XAttribute(Namespaces.Table + "operator", op));

    public void Read(XElement spreadsheet, Workbook workbook)
    {
        var claimedSheets = new HashSet<int>();
        var named = new List<(XElement Element, string Name, int SheetIndex, CellRange Range, bool Header, bool Buttons)>();
        foreach (var element in spreadsheet.Elements(Namespaces.Table + "database-ranges").Elements(Namespaces.Table + "database-range"))
        {
            if (_queries.TryRead(element, workbook)) continue;
            if (_ranges.Read((string?)element.Attribute(Namespaces.Table + "target-range-address")) is not { } target) continue;
            var sheetIndex = Index(workbook, target.Sheet);
            if (sheetIndex < 0) continue;

            var name = (string?)element.Attribute(Namespaces.Table + "name") ?? string.Empty;
            var header = (string?)element.Attribute(Namespaces.Table + "contains-header") != "false";
            var buttons = (string?)element.Attribute(Namespaces.Table + "display-filter-buttons") == "true";

            if (name.StartsWith(AnonymousPrefix, StringComparison.Ordinal))
            {
                if (claimedSheets.Add(sheetIndex)) ClaimSheetFilter(workbook, workbook.Sheets[sheetIndex], element, target.Range, header, buttons);
                continue;
            }
            named.Add((element, name, sheetIndex, target.Range, header, buttons));
        }

        foreach (var (element, name, sheetIndex, range, header, buttons) in named)
        {
            var footer = (string?)element.Attribute(Namespaces.Calcext + "contains-footer") == "true" && range.RowCount > (header ? 1 : 0);
            var defined = new DatabaseRange
            {
                Name = name, SheetIndex = sheetIndex, Range = range, HasHeader = header, HasTotalsRow = footer,
                Sort = _sorts.Read(element.Element(Namespaces.Table + "sort"), range),
            };
            if (buttons && claimedSheets.Add(sheetIndex))
                ClaimSheetFilter(workbook, workbook.Sheets[sheetIndex], element, range, header, buttons);
            else if (Stored(workbook, element, range) is { } stored)
                defined = defined with { ShowsFilterButtons = buttons, RangeFilter = stored };
            else
                defined = defined with { ShowsFilterButtons = buttons, Filters = [.. ReadFilters(workbook, workbook.Sheets[sheetIndex], element, range, header).Where(column => !column.IsEmpty)] };
            workbook.DatabaseRanges.Define(defined);
        }
    }

    private void ClaimSheetFilter(Workbook workbook, Worksheet sheet, XElement element, CellRange range, bool header, bool buttons)
    {
        if (buttons)
        {
            sheet.AutoFilter.Activate(range, header);
            sheet.AutoFilter.Remember(_sorts.Read(element.Element(Namespaces.Table + "sort"), range) is { LeftToRight: false } downward ? downward : null);
        }
        else if (_sorts.Read(element.Element(Namespaces.Table + "sort"), range) is { } plain)
            sheet.RangeSort = new SheetRangeSort(range, header, plain);
        if (Stored(workbook, element, range) is { } stored)
        {
            sheet.RangeFilter = new SheetRangeFilter(range, header, stored);
            return;
        }
        if (!buttons)
        {
            if (_columns.ToTree(ReadFilters(workbook, sheet, element, range, header)) is { } tree)
                sheet.RangeFilter = new SheetRangeFilter(range, header, new RangeFilter { Condition = tree });
            return;
        }
        foreach (var column in ReadFilters(workbook, sheet, element, range, header)) sheet.AutoFilter.Set(column);
    }

    private RangeFilter? Stored(Workbook workbook, XElement element, CellRange range)
    {
        var filter = element.Element(Namespaces.Table + "filter");
        if (filter is null) return null;
        if (_rangeFilters.ReadAdvanced(filter, sheet => Index(workbook, sheet)) is { } advanced)
            return advanced.Criteria is null && _rangeFilters.IsPlain(filter) && _rangeFilters.ReadTree(filter, range.ColumnCount) is { } unique
                ? advanced with { Condition = unique }
                : advanced;
        if (!_rangeFilters.IsPlain(filter)) return null;
        var tree = _rangeFilters.ReadTree(filter, range.ColumnCount);
        return tree is null || _rangeFilters.FitsColumns(tree) ? null : new RangeFilter { Condition = tree };
    }

    private IEnumerable<ColumnFilter> ReadFilters(Workbook workbook, Worksheet sheet, XElement range, CellRange area, bool header)
    {
        var columns = area.ColumnCount;
        var conditions = range.Element(Namespaces.Table + "filter")?.Descendants(Namespaces.Table + "filter-condition") ?? [];
        foreach (var group in conditions.GroupBy(condition => int.TryParse((string?)condition.Attribute(Namespaces.Table + "field-number"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var field) && field >= 0 && field < columns ? field : -1))
        {
            if (group.Key < 0) continue;
            var filter = new ColumnFilter(group.Key);
            var blanks = false;
            var filled = false;
            var criteria = new List<string>();
            var either = false;
            foreach (var condition in group)
            {
                var items = condition.Elements(Namespaces.Table + "filter-set-item").Select(item => (string?)item.Attribute(Namespaces.Table + "value") ?? string.Empty).ToList();
                var op = (string?)condition.Attribute(Namespaces.Table + "operator");
                var value = (string?)condition.Attribute(Namespaces.Table + "value") ?? string.Empty;
                if (_colors.FromOdf((string?)condition.Attribute(Namespaces.Loext + "data-type"), value) is { } color)
                    filter = filter with { Color = color };
                else if (_top.FromOdf(op, value) is { } top)
                    filter = filter with { Top = top };
                else if (op == "empty")
                    blanks = true;
                else if (op == "!empty")
                    filled = true;
                else if (items.Count > 0)
                    filter = filter with
                    {
                        AllowedValues = _dates.Resolve(items.Where(item => item.Length > 0), _dates.Read(workbook, sheet, area, group.Key, header)),
                        IncludeBlanks = items.Any(item => item.Length == 0),
                    };
                else if (criteria.Count < 2)
                {
                    criteria.Add(_criteria.FromOdf(op, value));
                    either |= condition.Parent?.Name == Namespaces.Table + "filter-or";
                }
            }
            filter = filter with
            {
                Criteria = criteria.ElementAtOrDefault(0),
                SecondCriteria = criteria.ElementAtOrDefault(1),
                Join = either && criteria.Count > 1 ? CriteriaJoin.Or : CriteriaJoin.And,
            };
            if (blanks && filled && filter.AllowedValues is null)
                filter = filter with { AllowedValues = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase), IncludeBlanks = false };
            else if (blanks) filter = filter with { AllowedValues = filter.AllowedValues ?? new HashSet<string>(StringComparer.CurrentCultureIgnoreCase), IncludeBlanks = true };
            else if (filled) filter = filter with { IncludeBlanks = false };
            yield return filter;
        }
    }

    private int Index(Workbook workbook, string sheet)
    {
        for (var i = 0; i < workbook.Sheets.Count; i++)
            if (string.Equals(workbook.Sheets[i].Name, sheet, StringComparison.Ordinal)) return i;
        return -1;
    }

}
