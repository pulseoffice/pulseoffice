using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfConsolidationMapper
{
    private readonly OdfQualifiedRanges _ranges = new();

    private readonly IReadOnlyList<(ConsolidationFunction Function, string Name)> _functions =
    [
        (ConsolidationFunction.Sum, "sum"), (ConsolidationFunction.Count, "count"), (ConsolidationFunction.Average, "average"),
        (ConsolidationFunction.Max, "max"), (ConsolidationFunction.Min, "min"), (ConsolidationFunction.Product, "product"),
        (ConsolidationFunction.CountNumbers, "countnums"), (ConsolidationFunction.StandardDeviation, "stdev"),
        (ConsolidationFunction.StandardDeviationPopulation, "stdevp"), (ConsolidationFunction.Variance, "var"),
        (ConsolidationFunction.VariancePopulation, "varp"),
    ];

    public XElement? Build(Workbook workbook)
    {
        if (workbook.Consolidation is not { } settings) return null;
        if (settings.TargetSheet < 0 || settings.TargetSheet >= workbook.Sheets.Count) return null;
        var sources = settings.Sources
            .Where(area => area.SheetIndex >= 0 && area.SheetIndex < workbook.Sheets.Count)
            .Select(area => _ranges.Write(workbook.Sheets[area.SheetIndex].Name, area.Range))
            .ToList();
        if (sources.Count == 0) return null;

        return new XElement(Namespaces.Table + "consolidation",
            new XAttribute(Namespaces.Table + "function", _functions.First(pair => pair.Function == settings.Function).Name),
            new XAttribute(Namespaces.Table + "source-cell-range-addresses", string.Join(' ', sources)),
            new XAttribute(Namespaces.Table + "target-cell-address", _ranges.WriteAddress(workbook.Sheets[settings.TargetSheet].Name, settings.Target)),
            new XAttribute(Namespaces.Table + "use-labels", Labels(settings)),
            settings.LinkToSource ? new XAttribute(Namespaces.Table + "link-to-source-data", "true") : null);
    }

    public void Read(XElement spreadsheet, Workbook workbook)
    {
        if (spreadsheet.Element(Namespaces.Table + "consolidation") is not { } element) return;
        if (_ranges.Read((string?)element.Attribute(Namespaces.Table + "target-cell-address")) is not { } target) return;
        var targetSheet = Index(workbook, target.Sheet);
        if (targetSheet < 0) return;
        var sources = _ranges.ReadAll((string?)element.Attribute(Namespaces.Table + "source-cell-range-addresses"))
            .Select(source => (Index: Index(workbook, source.Sheet), source.Range))
            .Where(source => source.Index >= 0)
            .Select(source => new ConsolidationArea(source.Index, source.Range))
            .ToList();
        if (sources.Count == 0) return;
        var labels = (string?)element.Attribute(Namespaces.Table + "use-labels") ?? "none";
        var function = (string?)element.Attribute(Namespaces.Table + "function") ?? "sum";

        workbook.Consolidation = new ConsolidationSettings
        {
            Sources = sources,
            TargetSheet = targetSheet,
            Target = target.Range.Start,
            Function = _functions.FirstOrDefault(pair => string.Equals(pair.Name, function, StringComparison.OrdinalIgnoreCase)) is { Name: not null } known
                ? known.Function
                : ConsolidationFunction.Sum,
            TopRowLabels = labels is "column" or "both",
            LeftColumnLabels = labels is "row" or "both",
            LinkToSource = (string?)element.Attribute(Namespaces.Table + "link-to-source-data") == "true",
        };
    }

    private string Labels(ConsolidationSettings settings) => (settings.TopRowLabels, settings.LeftColumnLabels) switch
    {
        (true, true) => "both",
        (true, false) => "column",
        (false, true) => "row",
        _ => "none",
    };

    private int Index(Workbook workbook, string sheet)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (string.Equals(workbook.Sheets[index].Name, sheet, StringComparison.Ordinal)) return index;
        return -1;
    }
}
