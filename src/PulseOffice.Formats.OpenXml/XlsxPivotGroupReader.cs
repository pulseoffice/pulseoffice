using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Pivot;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxPivotGroupReader
{
    private readonly IReadOnlyList<(GroupByValues By, DateGrouping Grouping)> _dateParts =
    [
        (GroupByValues.Seconds, DateGrouping.Seconds), (GroupByValues.Minutes, DateGrouping.Minutes),
        (GroupByValues.Hours, DateGrouping.Hours), (GroupByValues.Days, DateGrouping.Days),
        (GroupByValues.Months, DateGrouping.Months), (GroupByValues.Quarters, DateGrouping.Quarters),
        (GroupByValues.Years, DateGrouping.Years),
    ];

    public bool IsGrouped(CacheField cacheField) => cacheField.FieldGroup?.GetFirstChild<RangeProperties>() is not null;

    public int SourceIndexOf(CacheField cacheField, int index, int cacheFieldCount) =>
        cacheField.DatabaseField?.Value == false && cacheField.FieldGroup?.Base?.Value is { } baseIndex && baseIndex < cacheFieldCount
            ? (int)baseIndex
            : index;

    public PivotField Grouped(PivotField field, CacheField cacheField)
    {
        if (cacheField.FieldGroup?.GetFirstChild<RangeProperties>() is not { } range) return field;
        var by = range.GroupBy?.Value ?? GroupByValues.Range;
        if (by == GroupByValues.Range)
            return range.GroupInterval?.Value is { } interval and > 0 ? field with { NumericGroupInterval = interval } : field;
        foreach (var (value, grouping) in _dateParts)
            if (value == by) return field with { DateGrouping = grouping };
        return field;
    }

    public void MergeDateLevels(List<PivotField> fields)
    {
        var dated = fields.Where(field => field.DateGrouping != DateGrouping.None)
            .GroupBy(field => (field.Area, field.SourceColumnIndex))
            .Where(group => group.Count() > 1)
            .ToList();
        if (dated.Count == 0) return;

        foreach (var group in dated)
        {
            var members = group.ToList();
            var finest = members.Min(field => field.DateGrouping);
            var keep = fields.IndexOf(members[0]);
            fields[keep] = members[0] with { DateGrouping = finest };
            foreach (var extra in members.Skip(1)) fields.Remove(extra);
        }

        foreach (var area in fields.Select(field => field.Area).Distinct().ToList())
        {
            var order = 0;
            for (var at = 0; at < fields.Count; at++)
                if (fields[at].Area == area) fields[at] = fields[at] with { AreaOrder = order++ };
        }
    }
}
