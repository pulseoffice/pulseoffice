using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;
using PivotArea = PulseOffice.Sheets.Pivot.PivotArea;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxPivotGroupWriter
{
    private const double MinimumSerial = 1;
    private const double MaximumSerial = 2958466;

    private readonly IReadOnlyDictionary<DateGrouping, GroupByValues> _dateParts = new Dictionary<DateGrouping, GroupByValues>
    {
        [DateGrouping.Seconds] = GroupByValues.Seconds,
        [DateGrouping.Minutes] = GroupByValues.Minutes,
        [DateGrouping.Hours] = GroupByValues.Hours,
        [DateGrouping.Days] = GroupByValues.Days,
        [DateGrouping.Months] = GroupByValues.Months,
        [DateGrouping.Quarters] = GroupByValues.Quarters,
        [DateGrouping.Years] = GroupByValues.Years,
    };

    public bool CanWrite(PivotField field, IEnumerable<CellValue> values)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(values);
        if (field.DateGrouping == DateGrouping.None && field.NumericGroupInterval is null) return true;
        if (field.Area is not (PivotArea.Row or PivotArea.Column) || field.ExcludedValues.Count > 0) return false;
        var present = values.Where(value => !value.IsEmpty).ToList();
        if (present.Count == 0 || present.Any(value => value.Kind != ValueKind.Number)) return false;
        if (field.DateGrouping != DateGrouping.None)
            return present.All(value => value.AsNumber() is >= MinimumSerial and < MaximumSerial);
        return field.NumericGroupInterval is > 0;
    }

    public XlsxPivotGroups Plan(PivotDefinition pivot, int columnCount, Func<PivotField, int> indexOf, Func<int, IReadOnlyList<CellValue>> valuesOf, PulseOffice.Sheets.CalculationSettings calendar)
    {
        ArgumentNullException.ThrowIfNull(pivot);
        ArgumentNullException.ThrowIfNull(indexOf);
        ArgumentNullException.ThrowIfNull(valuesOf);

        var groups = new XlsxPivotGroups(columnCount);
        var planned = new HashSet<int>();
        foreach (var field in pivot.Fields.Where(field => !field.IsCalculated && field.Area is PivotArea.Row or PivotArea.Column))
        {
            var column = indexOf(field);
            if (column < 0 || !planned.Add(column)) continue;
            var values = valuesOf(column);
            if (field.DateGrouping == DateGrouping.None && field.NumericGroupInterval is null || !CanWrite(field, values)) continue;
            var numbers = values.Where(value => value.Kind == ValueKind.Number).Select(value => value.AsNumber()).ToList();
            if (field.DateGrouping != DateGrouping.None) PlanDates(groups, column, field.DateGrouping, numbers, calendar);
            else PlanRange(groups, column, field.NumericGroupInterval!.Value, numbers);
        }

        return groups;
    }

    private void PlanRange(XlsxPivotGroups groups, int column, double interval, List<double> numbers)
    {
        var bins = new NumericRangeBins();
        var start = bins.StartOf(numbers.Min(), interval);
        var end = numbers.Max();
        var count = (int)Math.Floor((end - start) / interval + 1e-9) + 1;
        var items = new List<string> { "<" + Format(start) };
        for (var bin = 0; bin < count; bin++)
            items.Add(Format(start + bin * interval) + "-" + Format(start + (bin + 1) * interval));
        items.Add(">" + Format(end));
        groups.Add(column, new XlsxPivotGroupLevel(null, new RangeProperties
        {
            StartNumber = start,
            EndNum = end,
            GroupInterval = interval,
        }, items));
    }

    private void PlanDates(XlsxPivotGroups groups, int column, DateGrouping grouping, List<double> numbers, PulseOffice.Sheets.CalculationSettings calendar)
    {
        var first = calendar.DateOf(Math.Floor(numbers.Min()));
        var last = calendar.DateOf(Math.Floor(numbers.Max()) + 1);
        var chain = new List<DateGrouping> { grouping };
        if (grouping < DateGrouping.Days) chain.Add(DateGrouping.Days);
        if (grouping != DateGrouping.Years) chain.Add(DateGrouping.Years);

        foreach (var (part, level) in chain.Select((part, level) => (part, level)))
            groups.Add(column, new XlsxPivotGroupLevel(
                level == 0 ? null : NameOf(part),
                new RangeProperties { GroupBy = _dateParts[part], StartDate = first, EndDate = last },
                [Bound('<', first), .. ItemsOf(part, first, last), Bound('>', last)]));
    }

    private string NameOf(DateGrouping part) => part switch
    {
        DateGrouping.Years => "Years",
        DateGrouping.Days => "Days",
        _ => part.ToString(),
    };

    private IEnumerable<string> ItemsOf(DateGrouping part, DateTime first, DateTime last) => part switch
    {
        DateGrouping.Years => Enumerable.Range(first.Year, Math.Max(1, last.AddDays(-1).Year - first.Year + 1)).Select(year => year.ToString(CultureInfo.InvariantCulture)),
        DateGrouping.Quarters => Enumerable.Range(1, 4).Select(quarter => "Qtr" + quarter.ToString(CultureInfo.InvariantCulture)),
        DateGrouping.Months => Enumerable.Range(1, 12).Select(month => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month)),
        DateGrouping.Days => Enumerable.Range(0, 366).Select(day => new DateTime(2000, 1, 1).AddDays(day).ToString("d-MMM", CultureInfo.InvariantCulture)),
        DateGrouping.Hours => Enumerable.Range(0, 24).Select(hour => new DateTime(2000, 1, 1, hour, 0, 0).ToString("h tt", CultureInfo.InvariantCulture)),
        _ => Enumerable.Range(0, 60).Select(unit => ":" + unit.ToString("00", CultureInfo.InvariantCulture)),
    };

    private string Bound(char side, DateTime date) => side + date.ToString("M/d/yyyy", CultureInfo.InvariantCulture);

    private string Format(double value) => Math.Round(value, 12).ToString("0.############", CultureInfo.InvariantCulture);

    private sealed class NumericRangeBins
    {
        public double StartOf(double number, double interval)
        {
            var ratio = number / interval;
            var nearest = Math.Round(ratio);
            var index = Math.Abs(ratio - nearest) <= 1e-9 * Math.Max(1, Math.Abs(ratio)) ? nearest : Math.Floor(ratio);
            return index * interval;
        }
    }
}

public sealed record XlsxPivotGroupLevel(string? DerivedName, RangeProperties Range, IReadOnlyList<string> Items);

public sealed class XlsxPivotGroups(int columnCount)
{
    private readonly Dictionary<int, List<XlsxPivotGroupLevel>> _levels = [];
    private readonly List<(int Base, XlsxPivotGroupLevel Level)> _derived = [];

    public int DerivedCount => _derived.Count;

    public bool IsGrouped(int column) => _levels.ContainsKey(column);

    public bool HasDates(int column) => _levels.TryGetValue(column, out var levels) && levels[0].Range.GroupBy?.Value is { } by && by != GroupByValues.Range;

    public void Add(int column, XlsxPivotGroupLevel level)
    {
        if (!_levels.TryGetValue(column, out var levels)) _levels[column] = levels = [];
        levels.Add(level);
        if (levels.Count > 1) _derived.Add((column, level));
    }

    public FieldGroup? GroupOf(int column) =>
        _levels.TryGetValue(column, out var levels) ? Group(column, levels[0], ParentOf(column, 0)) : null;

    public IEnumerable<CacheField> DerivedFields() =>
        _derived.Select((entry, index) => new CacheField(Group(entry.Base, entry.Level, ParentOf(entry.Base, _levels[entry.Base].IndexOf(entry.Level))))
        {
            Name = entry.Level.DerivedName,
            NumberFormatId = 0,
            DatabaseField = false,
        });

    public int ItemCountOf(int field) =>
        field < columnCount
            ? _levels.TryGetValue(field, out var levels) ? levels[0].Items.Count : -1
            : field - columnCount < _derived.Count ? _derived[field - columnCount].Level.Items.Count : -1;

    public bool IsDerived(int field) => field >= columnCount && field - columnCount < _derived.Count;

    public int BaseOf(int field) => IsDerived(field) ? _derived[field - columnCount].Base : field;

    public IReadOnlyList<int> Expand(int column)
    {
        if (!_levels.TryGetValue(column, out var levels) || levels.Count == 1) return [column];
        var outer = levels.Skip(1).Reverse().Select(level => columnCount + _derived.FindIndex(entry => ReferenceEquals(entry.Level, level)));
        return [.. outer, column];
    }

    private uint? ParentOf(int column, int level)
    {
        var levels = _levels[column];
        if (level + 1 >= levels.Count) return null;
        return (uint)(columnCount + _derived.FindIndex(entry => ReferenceEquals(entry.Level, levels[level + 1])));
    }

    private FieldGroup Group(int column, XlsxPivotGroupLevel level, uint? parent)
    {
        var items = new GroupItems(level.Items.Select(item => new StringItem { Val = item })) { Count = (uint)level.Items.Count };
        return new FieldGroup((RangeProperties)level.Range.CloneNode(true), items) { Base = (uint)column, ParentId = parent };
    }
}
