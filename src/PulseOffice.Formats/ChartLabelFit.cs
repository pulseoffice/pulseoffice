using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats;

public sealed class ChartLabelFit
{
    public ChartDefinition Fit(ChartDefinition definition, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition with
        {
            FirstColumnIsCategory = definition.FirstColumnIsCategory && (!definition.SeriesInColumns || range.ColumnCount > 1),
            FirstRowIsHeader = definition.FirstRowIsHeader && (definition.SeriesInColumns || range.RowCount > 1),
        };
    }

    public ChartDefinition Fit(ChartDefinition definition) =>
        CellRange.TryParseA1(definition?.SourceRange ?? string.Empty, out var range) ? Fit(definition!, range) : definition!;
}
