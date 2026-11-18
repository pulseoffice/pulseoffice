using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxDynamicFilterReader
{
    public ColumnFilter Apply(ColumnFilter filter, DynamicFilter dynamic)
    {
        if (dynamic.Val?.Value is not { } value || !double.IsFinite(value)) return filter;
        var type = dynamic.Type?.Value;
        if (type == DynamicFilterValues.AboveAverage) return filter with { Criteria = ">" + Text(value) };
        if (type == DynamicFilterValues.BelowAverage) return filter with { Criteria = "<" + Text(value) };
        if (dynamic.MaxVal?.Value is not { } max || !double.IsFinite(max)) return filter;
        return filter with { Criteria = ">=" + Text(value), SecondCriteria = "<" + Text(max), Join = CriteriaJoin.And };
    }

    private string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
