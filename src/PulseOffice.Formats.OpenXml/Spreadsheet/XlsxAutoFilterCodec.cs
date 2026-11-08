using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Sheet = PulseOffice.Sheets.Worksheet;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml;

internal sealed record XlsxFilterSource(Workbook Workbook, Sheet Sheet, bool HasHeader);

internal sealed class XlsxAutoFilterCodec
{
    private readonly IReadOnlyList<(string Prefix, FilterOperatorValues Sdk)> _operators =
    [
        (">=", FilterOperatorValues.GreaterThanOrEqual), ("<=", FilterOperatorValues.LessThanOrEqual),
        ("<>", FilterOperatorValues.NotEqual), (">", FilterOperatorValues.GreaterThan), ("<", FilterOperatorValues.LessThan),
        ("=", FilterOperatorValues.Equal),
    ];

    private const uint MaxColumnOffset = 16383;

    private readonly SpreadsheetColours _colours = new();

    private readonly XlsxFilterDateGroups _dates = new();

    private readonly FilterColumnDates _columnDates = new();

    private readonly XlsxSortStateCodec _sorts = new();

    public AutoFilter? Build(Workbook workbook, Sheet sheet, XlsxConditionalMapper formats)
    {
        if (sheet.AutoFilter.Range is not { } range) return null;
        var filter = Build(range, sheet.AutoFilter.Filters, formats, new XlsxFilterSource(workbook, sheet, sheet.AutoFilter.HasHeader));
        if (_sorts.Build(range, sheet.AutoFilter.HasHeader, sheet.AutoFilter.Sort) is { } sort) filter.Append(sort);
        return filter;
    }

    public AutoFilter Build(CellRange range, IEnumerable<PulseOffice.Sheets.ColumnFilter> columns, XlsxConditionalMapper formats, XlsxFilterSource? source = null)
    {
        var filter = new AutoFilter { Reference = $"{range.Start.ToA1()}:{range.End.ToA1()}" };
        foreach (var column in columns.OrderBy(entry => entry.ColumnOffset))
        {
            var element = new FilterColumn { ColumnId = (uint)column.ColumnOffset };
            if (column.Color is { } color)
                element.Append(new ColorFilter
                {
                    FormatId = formats.InternColour(color.ColorHex ?? (color.Target == ColorFilterTarget.Background ? "#FFFFFF" : CellFont.Default.ColorHex)),
                    CellColor = color.Target == ColorFilterTarget.Background ? null : false,
                });
            else if (column.AllowedValues is { } allowed)
            {
                var filters = new Filters();
                if (column.IncludeBlanks) filters.Blank = true;
                var dates = source is null
                    ? new Dictionary<string, DateTime>()
                    : _columnDates.Read(source.Workbook, source.Sheet, range, column.ColumnOffset, source.HasHeader);
                foreach (var value in allowed.Where(value => !dates.ContainsKey(value)).Order(StringComparer.Ordinal)) filters.Append(new Filter { Val = value });
                foreach (var moment in allowed.Where(dates.ContainsKey).Select(value => dates[value]).Distinct().Order())
                    filters.Append(_dates.Item(moment));
                element.Append(filters);
            }
            else if (column.Top is { } top)
                element.Append(new Top10
                {
                    Top = top.Bottom ? false : null,
                    Percent = top.Percent ? true : null,
                    Val = top.Count,
                });
            else if (column.Conditions is { Count: > 0 } conditions)
            {
                var custom = new CustomFilters(conditions.Select(Custom));
                if (conditions.Count > 1 && column.Join == CriteriaJoin.And) custom.And = true;
                if (!column.IncludeBlanks && conditions.Count == 1)
                {
                    custom.And = true;
                    custom.Append(NonBlank());
                }
                element.Append(custom);
            }
            else if (!column.IncludeBlanks)
                element.Append(new CustomFilters(NonBlank()));
            filter.Append(element);
        }
        return filter;
    }

    public void Read(AutoFilter? element, Sheet sheet, IReadOnlyList<DifferentialFormat> formats)
    {
        if (Read(element, formats, sheet) is not var (range, filters)) return;
        sheet.AutoFilter.Activate(range);
        foreach (var filter in filters) sheet.AutoFilter.Set(filter);
    }

    public (CellRange Range, IReadOnlyList<PulseOffice.Sheets.ColumnFilter> Filters)? Read(AutoFilter? element, IReadOnlyList<DifferentialFormat> formats, Sheet? sheet = null)
    {
        if (element?.Reference?.Value is not { } reference || !CellRange.TryParseA1(reference, out var range)) return null;
        var read = new List<PulseOffice.Sheets.ColumnFilter>();
        foreach (var column in element.Elements<FilterColumn>())
        {
            if (column.ColumnId?.Value is > MaxColumnOffset) continue;
            var filter = new PulseOffice.Sheets.ColumnFilter((int)(column.ColumnId?.Value ?? 0));
            if (column.GetFirstChild<ColorFilter>() is { } colour)
                filter = filter with { Color = Colour(colour, formats) };
            else if (column.GetFirstChild<Filters>() is { } filters)
                filter = filter with
                {
                    AllowedValues = filters.Elements<Filter>().Select(f => f.Val?.Value ?? string.Empty)
                        .Concat(_dates.Matching(filters, sheet, range, filter.ColumnOffset))
                        .ToHashSet(StringComparer.CurrentCultureIgnoreCase),
                    IncludeBlanks = filters.Blank?.Value == true,
                };
            else if (column.GetFirstChild<Top10>() is { } top)
                filter = filter with { Top = new TopItemsCriterion(top.Top?.Value == false, top.Percent?.Value == true, top.Val?.Value is { } count && double.IsFinite(count) ? Math.Clamp(count, 0, 1048576) : 10) };
            else if (column.GetFirstChild<CustomFilters>() is { } customs)
            {
                var conditions = new List<string>();
                var conjunctive = customs.And?.Value == true || customs.Elements<CustomFilter>().Count() == 1;
                foreach (var custom in customs.Elements<CustomFilter>())
                {
                    if (IsNonBlank(custom) && conjunctive) filter = filter with { IncludeBlanks = false };
                    else if (conditions.Count < 2) conditions.Add(Criteria(custom));
                }
                filter = filter with
                {
                    Criteria = conditions.ElementAtOrDefault(0),
                    SecondCriteria = conditions.ElementAtOrDefault(1),
                    Join = conditions.Count > 1 && customs.And?.Value != true ? CriteriaJoin.Or : CriteriaJoin.And,
                };
            }
            if (!filter.IsEmpty) read.RemoveAll(existing => existing.ColumnOffset == filter.ColumnOffset);
            if (!filter.IsEmpty) read.Add(filter);
        }
        return (range, read);
    }

    public bool LosesBlankExclusion(PulseOffice.Sheets.ColumnFilter column) =>
        column.Color is null && column.AllowedValues is null && column.Top is null && column.Conditions.Count > 1 && !column.IncludeBlanks;

    private CustomFilter Custom(string criteria)
    {
        var match = _operators.FirstOrDefault(op => criteria.StartsWith(op.Prefix, StringComparison.Ordinal));
        var value = match.Prefix is null ? criteria : criteria[match.Prefix.Length..];
        return new CustomFilter { Operator = match.Prefix is null ? FilterOperatorValues.Equal : match.Sdk, Val = value };
    }

    private string Criteria(CustomFilter custom)
    {
        var op = custom.Operator?.Value is { } sdk ? _operators.FirstOrDefault(entry => entry.Sdk == sdk).Prefix ?? "=" : "=";
        var value = custom.Val?.Value ?? string.Empty;
        return op == "=" && value.Length > 0 ? value : op + value;
    }

    private CustomFilter NonBlank() => new() { Operator = FilterOperatorValues.NotEqual, Val = " " };

    private bool IsNonBlank(CustomFilter custom) =>
        custom.Operator?.Value == FilterOperatorValues.NotEqual && string.IsNullOrWhiteSpace(custom.Val?.Value);

    private ColorFilterCriterion Colour(ColorFilter filter, IReadOnlyList<DifferentialFormat> formats)
    {
        var background = filter.CellColor?.Value != false;
        var format = filter.FormatId?.Value is { } id && id < formats.Count ? formats[(int)id] : null;
        var fill = format?.Fill?.PatternFill;
        var font = _colours.Parse(format?.Font?.Color?.Rgb?.Value);
        var hex = background
            ? _colours.Parse(fill?.ForegroundColor?.Rgb?.Value) ?? _colours.Parse(fill?.BackgroundColor?.Rgb?.Value)
            : font ?? _colours.Parse(fill?.ForegroundColor?.Rgb?.Value);
        return new ColorFilterCriterion(background ? ColorFilterTarget.Background : ColorFilterTarget.Text, hex);
    }
}
