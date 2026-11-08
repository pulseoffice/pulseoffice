using PulseOffice.Formats;
using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfPivotMapper
{
    private readonly XNamespace TableOoo = OdfTableStyleMapper.TableOoo;

    private readonly OdfQualifiedRanges _ranges = new();
    private readonly OdfPivotSourceCodec _external = new();
    private readonly TokenMap<PivotArea> _areas = new(PivotArea.Unused, "hidden",
        (PivotArea.Row, "row"), (PivotArea.Column, "column"), (PivotArea.Value, "data"), (PivotArea.Filter, "page"));
    private readonly TokenMap<PivotAggregation> _aggregations = new(PivotAggregation.Sum, "sum",
        (PivotAggregation.Count, "count"), (PivotAggregation.CountNumbers, "countnums"), (PivotAggregation.Average, "average"),
        (PivotAggregation.Max, "max"), (PivotAggregation.Min, "min"), (PivotAggregation.Product, "product"),
        (PivotAggregation.StandardDeviation, "stdev"), (PivotAggregation.StandardDeviationPopulation, "stdevp"),
        (PivotAggregation.Variance, "var"), (PivotAggregation.VariancePopulation, "varp"), (PivotAggregation.Median, "median"));
    private readonly TokenMap<ValueDisplayMode> _references = new(ValueDisplayMode.Normal, "none",
        (ValueDisplayMode.PercentOfTotal, "total-percentage"), (ValueDisplayMode.PercentOfRow, "row-percentage"),
        (ValueDisplayMode.PercentOfColumn, "column-percentage"), (ValueDisplayMode.RunningTotal, "running-total"),
        (ValueDisplayMode.DifferenceFrom, "member-difference"),
        (ValueDisplayMode.PercentDifferenceFrom, "member-percentage-difference"), (ValueDisplayMode.Index, "index"));
    private readonly TokenMap<DateGrouping> _dateParts = new(DateGrouping.None, "",
        (DateGrouping.Seconds, "seconds"), (DateGrouping.Minutes, "minutes"), (DateGrouping.Hours, "hours"),
        (DateGrouping.Days, "days"), (DateGrouping.Months, "months"), (DateGrouping.Quarters, "quarters"),
        (DateGrouping.Years, "years"));

    public IReadOnlySet<PivotAggregation> WrittenAggregations { get; } = new HashSet<PivotAggregation>
    {
        PivotAggregation.Sum, PivotAggregation.Count, PivotAggregation.CountNumbers, PivotAggregation.Average,
        PivotAggregation.Max, PivotAggregation.Min, PivotAggregation.Product, PivotAggregation.StandardDeviation,
        PivotAggregation.StandardDeviationPopulation, PivotAggregation.Variance, PivotAggregation.VariancePopulation,
        PivotAggregation.Median,
    };

    public IReadOnlySet<ValueDisplayMode> WrittenDisplayModes { get; } = new HashSet<ValueDisplayMode>
    {
        ValueDisplayMode.Normal, ValueDisplayMode.PercentOfTotal, ValueDisplayMode.PercentOfRow,
        ValueDisplayMode.PercentOfColumn, ValueDisplayMode.RunningTotal, ValueDisplayMode.DifferenceFrom,
        ValueDisplayMode.PercentDifferenceFrom, ValueDisplayMode.Index,
    };

    public XElement? Build(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (workbook.Pivots.Count == 0) return null;

        var container = new XElement(Namespaces.Table + "data-pilot-tables");

        foreach (var pivot in workbook.Pivots)
        {
            var element = new XElement(Namespaces.Table + "data-pilot-table",
                new XAttribute(Namespaces.Table + "name", pivot.Name),
                new XAttribute(Namespaces.Table + "application-data", pivot.Id),
                new XAttribute(Namespaces.Table + "target-range-address", TargetOf(pivot)),
                new XAttribute(Namespaces.Table + "grand-total", GrandTotal(pivot)),
                new XAttribute(Namespaces.Table + "show-filter-button", "false"),
                new XAttribute(Namespaces.Table + "drill-down-on-double-click", "false"),
                pivot.ExternalSource is null
                    ? new XElement(Namespaces.Table + "source-cell-range",
                        new XAttribute(Namespaces.Table + "cell-range-address", _ranges.Write(pivot.SourceSheet, pivot.SourceRange)))
                    : _external.BuildSource(pivot));

            var baseField = pivot.In(PivotArea.Row).Concat(pivot.In(PivotArea.Column)).FirstOrDefault();
            var placed = pivot.Fields
                .Where(field => field.Area != PivotArea.Unused && !field.IsCalculated)
                .OrderBy(field => field.Area)
                .ThenBy(field => field.AreaOrder);

            foreach (var field in placed) element.Add(BuildField(pivot, field, baseField));
            if (pivot.ExternalSource is { } external) element.Add(_external.BuildCache(external));

            container.Add(element);
        }

        return container;
    }

    private XElement BuildField(PivotDefinition pivot, PivotField field, PivotField? baseField)
    {
        var isValue = field.Area == PivotArea.Value;
        var element = new XElement(Namespaces.Table + "data-pilot-field",
            new XAttribute(Namespaces.Table + "source-field-name", field.SourceName),
            new XAttribute(Namespaces.Table + "orientation", _areas.Token(field.Area)),
            new XAttribute(Namespaces.Table + "function", isValue ? _aggregations.Token(field.Aggregation) : "auto"));

        if (field.DisplayName is { Length: > 0 } label) element.Add(new XAttribute(TableOoo + "display-name", label));

        if (!isValue) element.Add(BuildLevel(pivot, field));

        if (isValue && field.DisplayMode != ValueDisplayMode.Normal && WrittenDisplayModes.Contains(field.DisplayMode))
        {
            var reference = new XElement(Namespaces.Table + "data-pilot-field-reference",
                new XAttribute(Namespaces.Table + "field-name", baseField?.SourceName ?? string.Empty),
                new XAttribute(Namespaces.Table + "type", _references.Token(field.DisplayMode)));
            if (field.DisplayMode is ValueDisplayMode.DifferenceFrom or ValueDisplayMode.PercentDifferenceFrom)
                reference.Add(new XAttribute(Namespaces.Table + "member-type", "previous"));
            element.Add(reference);
        }

        if (!isValue && Grouping(field) is { } groups) element.Add(groups);

        return element;
    }

    private XElement BuildLevel(PivotDefinition pivot, PivotField field)
    {
        var level = new XElement(Namespaces.Table + "data-pilot-level",
            new XAttribute(Namespaces.Table + "show-empty", field.ShowItemsWithNoData ? "true" : "false"),
            new XAttribute(Namespaces.Calcext + "repeat-item-labels", pivot.RepeatItemLabels ? "true" : "false"));

        if (field.ShowSubtotals)
            level.Add(new XElement(Namespaces.Table + "data-pilot-subtotals",
                new XElement(Namespaces.Table + "data-pilot-subtotal", new XAttribute(Namespaces.Table + "function", "auto"))));

        if (field.ExcludedValues.Count > 0)
            level.Add(new XElement(Namespaces.Table + "data-pilot-members",
                field.ExcludedValues.Select(value => new XElement(Namespaces.Table + "data-pilot-member",
                    new XAttribute(Namespaces.Table + "name", value),
                    new XAttribute(Namespaces.Table + "display", "false"),
                    new XAttribute(Namespaces.Table + "show-details", "true")))));

        level.Add(new XElement(Namespaces.Table + "data-pilot-sort-info",
            new XAttribute(Namespaces.Table + "sort-mode", "name"),
            new XAttribute(Namespaces.Table + "order", field.SortDescending ? "descending" : "ascending")));

        var layout = new XElement(Namespaces.Table + "data-pilot-layout-info",
            new XAttribute(Namespaces.Table + "add-empty-lines", "false"),
            new XAttribute(Namespaces.Table + "layout-mode", pivot.Layout == PivotLayout.Tabular ? "tabular-layout" : "outline-subtotals-top"));
        if (pivot.Layout == PivotLayout.Compact) layout.Add(new XAttribute(Namespaces.Loext + "layout-mode", "compact-layout"));
        level.Add(layout);

        return level;
    }

    private XElement? Grouping(PivotField field)
    {
        if (field.DateGrouping != DateGrouping.None)
            return new XElement(Namespaces.Table + "data-pilot-groups",
                new XAttribute(Namespaces.Table + "date-start", "auto"),
                new XAttribute(Namespaces.Table + "date-end", "auto"),
                new XAttribute(Namespaces.Table + "step", "0"),
                new XAttribute(Namespaces.Table + "grouped-by", _dateParts.Token(field.DateGrouping)));

        return field.NumericGroupInterval is { } interval and > 0
            ? new XElement(Namespaces.Table + "data-pilot-groups",
                new XAttribute(Namespaces.Table + "start", "auto"),
                new XAttribute(Namespaces.Table + "end", "auto"),
                new XAttribute(Namespaces.Table + "step", interval.ToString("R", CultureInfo.InvariantCulture)))
            : null;
    }

    public OdfPivotReadReport Read(XElement content, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(workbook);

        var passwords = 0;
        var filtered = 0;
        foreach (var element in content.Descendants(Namespaces.Table + "data-pilot-table"))
        {
            var target = element.Attribute(Namespaces.Table + "target-range-address")?.Value;
            if (target is null || _ranges.Read(target) is not { } targetRange) continue;

            OdfPivotExternalRead? external = null;
            (string Sheet, CellRange Range) sourceRange;
            List<string> headers;
            if (_external.IsExternal(element))
            {
                if ((external = _external.Read(element)) is null) continue;
                if (external.DroppedPassword) passwords++;
                sourceRange = (external.Sheet, external.Range);
                headers = [.. external.Source.Cache.Headers];
            }
            else
            {
                var source = element.Element(Namespaces.Table + "source-cell-range")?.Attribute(Namespaces.Table + "cell-range-address")?.Value;
                if (source is null || _ranges.Read(source) is not { } local) continue;
                if (element.Element(Namespaces.Table + "source-cell-range")?.Element(Namespaces.Table + "filter")?.Descendants(Namespaces.Table + "filter-condition").Any() == true) filtered++;
                sourceRange = local;
                headers = Headers(workbook, local.Sheet, local.Range);
            }

            var fields = new List<PivotField>();
            var order = new Dictionary<PivotArea, int>();
            var layout = PivotLayout.Compact;
            var repeat = false;

            foreach (var field in element.Elements(Namespaces.Table + "data-pilot-field"))
            {
                if (field.Attribute(Namespaces.Table + "is-data-layout-field")?.Value == "true") continue;

                var name = field.Attribute(Namespaces.Table + "source-field-name")?.Value;
                if (name is null) continue;

                var area = _areas.Value(field.Attribute(Namespaces.Table + "orientation")?.Value);
                if (area == PivotArea.Unused) continue;

                order.TryGetValue(area, out var position);
                order[area] = position + 1;

                var level = field.Element(Namespaces.Table + "data-pilot-level");
                var groups = field.Element(Namespaces.Table + "data-pilot-groups");
                var reference = field.Element(Namespaces.Table + "data-pilot-field-reference");
                var layoutInfo = level?.Element(Namespaces.Table + "data-pilot-layout-info");
                if (layoutInfo is not null) layout = Layout(layoutInfo);
                if (level?.Attribute(Namespaces.Calcext + "repeat-item-labels")?.Value == "true") repeat = true;

                fields.Add(new PivotField
                {
                    SourceName = name,
                    DisplayName = field.Attribute(TableOoo + "display-name")?.Value,
                    SourceColumnIndex = Math.Max(headers.IndexOf(name), 0),
                    Area = area,
                    AreaOrder = position,
                    Aggregation = Aggregation(field.Attribute(Namespaces.Table + "function")?.Value),
                    DisplayMode = _references.Value(reference?.Attribute(Namespaces.Table + "type")?.Value),
                    ShowSubtotals = level is null || Subtotals(level),
                    ShowItemsWithNoData = level?.Attribute(Namespaces.Table + "show-empty")?.Value == "true",
                    SortDescending = level?.Element(Namespaces.Table + "data-pilot-sort-info")?
                        .Attribute(Namespaces.Table + "order")?.Value == "descending",
                    ExcludedValues = level?.Element(Namespaces.Table + "data-pilot-members")?
                        .Elements(Namespaces.Table + "data-pilot-member")
                        .Where(member => member.Attribute(Namespaces.Table + "display")?.Value == "false")
                        .Select(member => member.Attribute(Namespaces.Table + "name")?.Value)
                        .OfType<string>()
                        .ToHashSet() ?? [],
                    DateGrouping = groups?.Attribute(Namespaces.Table + "source-field-name") is null
                        ? _dateParts.Value(groups?.Attribute(Namespaces.Table + "grouped-by")?.Value)
                        : DateGrouping.None,
                    NumericGroupInterval = NumericInterval(groups),
                });
            }

            if (fields.Count == 0) continue;

            var grand = element.Attribute(Namespaces.Table + "grand-total")?.Value ?? "both";

            workbook.Pivots.Add(new PivotDefinition
            {
                Id = element.Attribute(Namespaces.Table + "application-data")?.Value
                    ?? Guid.NewGuid().ToString("N"),
                Name = element.Attribute(Namespaces.Table + "name")?.Value ?? "Pivot",
                SourceSheet = sourceRange.Sheet,
                SourceRange = sourceRange.Range,
                ExternalSource = external?.Source,
                DestinationSheet = targetRange.Sheet,
                Destination = targetRange.Range.Start,
                Fields = fields,
                OutputRange = targetRange.Range,
                Layout = layout,
                RepeatItemLabels = repeat,
                ShowGrandTotalRows = grand is "both" or "column",
                ShowGrandTotalColumns = grand is "both" or "row",
            });
        }
        return new OdfPivotReadReport(passwords, filtered);
    }

    private PivotAggregation Aggregation(string? token) => token is null or "auto" ? PivotAggregation.Sum : _aggregations.Value(token);

    private bool Subtotals(XElement level) =>
        level.Element(Namespaces.Table + "data-pilot-subtotals")?
            .Elements(Namespaces.Table + "data-pilot-subtotal")
            .Any(subtotal => subtotal.Attribute(Namespaces.Table + "function")?.Value != "none") ?? false;

    private PivotLayout Layout(XElement info) =>
        info.Attribute(Namespaces.Loext + "layout-mode")?.Value == "compact-layout" ? PivotLayout.Compact
        : info.Attribute(Namespaces.Table + "layout-mode")?.Value == "tabular-layout" ? PivotLayout.Tabular
        : PivotLayout.Outline;

    private double? NumericInterval(XElement? groups)
    {
        if (groups is null || groups.Attribute(Namespaces.Table + "grouped-by") is not null) return null;
        if (groups.Attribute(Namespaces.Table + "source-field-name") is not null) return null;
        return double.TryParse(groups.Attribute(Namespaces.Table + "step")?.Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var step) && double.IsFinite(step) && step > 0 ? step : null;
    }

    private string GrandTotal(PivotDefinition pivot) => (pivot.ShowGrandTotalColumns, pivot.ShowGrandTotalRows) switch
    {
        (true, true) => "both",
        (true, false) => "row",
        (false, true) => "column",
        _ => "none",
    };

    private string TargetOf(PivotDefinition pivot) =>
        _ranges.Write(pivot.DestinationSheet, pivot.OutputRange ?? new CellRange(pivot.Destination, pivot.Destination));

    private List<string> Headers(Workbook workbook, string sheetName, CellRange range) =>
        workbook.FindSheet(sheetName) is not { } sheet
            ? []
            : [.. Enumerable.Range(range.Start.Column, range.End.Column - range.Start.Column + 1)
                .Select(column => sheet.GetValue(new CellAddress(range.Start.Row, column)).ToString())];
}

public sealed record OdfPivotReadReport(int ServicePasswords, int SourceFilters);
