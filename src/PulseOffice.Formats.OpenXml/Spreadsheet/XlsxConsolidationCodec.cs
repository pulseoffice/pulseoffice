using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Book = PulseOffice.Sheets.Workbook;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxConsolidationCodec
{
    private readonly IReadOnlyList<(ConsolidationFunction Function, DataConsolidateFunctionValues Value)> _functions =
    [
        (ConsolidationFunction.Sum, DataConsolidateFunctionValues.Sum), (ConsolidationFunction.Count, DataConsolidateFunctionValues.Count),
        (ConsolidationFunction.Average, DataConsolidateFunctionValues.Average), (ConsolidationFunction.Max, DataConsolidateFunctionValues.Maximum),
        (ConsolidationFunction.Min, DataConsolidateFunctionValues.Minimum), (ConsolidationFunction.Product, DataConsolidateFunctionValues.Product),
        (ConsolidationFunction.CountNumbers, DataConsolidateFunctionValues.CountNumbers),
        (ConsolidationFunction.StandardDeviation, DataConsolidateFunctionValues.StandardDeviation),
        (ConsolidationFunction.StandardDeviationPopulation, DataConsolidateFunctionValues.StandardDeviationP),
        (ConsolidationFunction.Variance, DataConsolidateFunctionValues.Variance),
        (ConsolidationFunction.VariancePopulation, DataConsolidateFunctionValues.VarianceP),
    ];

    public DataConsolidate? Build(Book workbook, Sheet sheet)
    {
        if (workbook.Consolidation is not { } settings || settings.TargetSheet != IndexOf(workbook, sheet)) return null;
        var references = settings.Sources
            .Where(area => area.SheetIndex >= 0 && area.SheetIndex < workbook.Sheets.Count)
            .Select(area => new DataReference { Reference = area.Range.ToString(), Sheet = workbook.Sheets[area.SheetIndex].Name })
            .ToList();
        if (references.Count == 0) return null;

        var element = new DataConsolidate
        {
            Function = settings.Function == ConsolidationFunction.Sum ? null : _functions.First(pair => pair.Function == settings.Function).Value,
            LeftLabels = settings.LeftColumnLabels ? true : null,
            TopLabels = settings.TopRowLabels ? true : null,
            Link = settings.LinkToSource ? true : null,
        };
        element.Append(new DataReferences(references) { Count = (uint)references.Count });
        return element;
    }

    public void Read(DataConsolidate? element, Sheet sheet, Book workbook)
    {
        if (element is null || workbook.Consolidation is not null) return;
        var sources = element.DataReferences?.Elements<DataReference>()
            .Select(reference => (Index: workbook.FindSheet(reference.Sheet?.Value ?? sheet.Name) is { } source ? workbook.IndexOf(source) : -1,
                Text: reference.Reference?.Value))
            .Where(reference => reference.Index >= 0 && reference.Text is { Length: > 0 } && CellRange.TryParseA1(reference.Text.Replace("$", string.Empty, StringComparison.Ordinal), out _))
            .Select(reference =>
            {
                CellRange.TryParseA1(reference.Text!.Replace("$", string.Empty, StringComparison.Ordinal), out var range);
                return new ConsolidationArea(reference.Index, range);
            })
            .ToList() ?? [];
        if (sources.Count == 0) return;
        var function = element.Function?.Value;

        workbook.Consolidation = new ConsolidationSettings
        {
            Sources = sources,
            TargetSheet = workbook.IndexOf(sheet),
            Target = CellAddress.First,
            Function = function is { } value
                ? _functions.Where(pair => pair.Value == value).Select(pair => (ConsolidationFunction?)pair.Function).FirstOrDefault() ?? ConsolidationFunction.Sum
                : ConsolidationFunction.Sum,
            LeftColumnLabels = element.LeftLabels?.Value == true || element.StartLabels?.Value == true,
            TopRowLabels = element.TopLabels?.Value == true,
            LinkToSource = element.Link?.Value == true,
        };
    }

    private int IndexOf(Book workbook, Sheet sheet)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (ReferenceEquals(workbook.Sheets[index], sheet)) return index;
        return -1;
    }
}
