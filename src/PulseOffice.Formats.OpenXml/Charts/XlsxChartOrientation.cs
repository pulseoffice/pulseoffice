using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxChartOrientation
{
    public ChartDefinition Infer(IReadOnlyList<OpenXmlCompositeElement> series, ChartDefinition definition)
    {
        if (series.Count == 0) return definition;

        var first = series[0];
        var named = first.GetFirstChild<C.SeriesText>()?.GetFirstChild<C.StringReference>() is not null;
        var categorised = first.GetFirstChild<C.CategoryAxisData>()?.Descendants<C.Formula>().Any() == true;
        var values = Range(first.GetFirstChild<C.Values>()?.Descendants<C.Formula>().FirstOrDefault()?.Text);
        var inRows = values is { RowCount: 1, ColumnCount: > 1 };

        return definition with
        {
            SeriesInColumns = !inRows,
            FirstRowIsHeader = inRows ? categorised : named,
            FirstColumnIsCategory = inRows ? named : categorised,
        };
    }

    private CellRange? Range(string? formula)
    {
        if (formula is null) return null;
        var reference = formula[(formula.LastIndexOf('!') + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
        return CellRange.TryParseA1(reference, out var range) ? range : null;
    }
}
