using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerCommands(
    LedgerCellFormatCommands cellFormat,
    LedgerPaneCommands panes,
    LedgerEditDataCommands editData,
    LedgerConditionalRuleCommands conditional,
    LedgerChartCommands charts,
    LedgerShapeCommands shapes,
    LedgerShapeListCommands shapeLists,
    LedgerShapeColourCommands shapeColours)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. cellFormat.GetCommands(),
        .. panes.GetCommands(),
        .. editData.GetCommands(),
        .. conditional.GetCommands(),
        .. charts.GetCommands(),
        .. shapes.GetCommands(),
        .. shapeLists.GetCommands(),
        .. shapeColours.GetCommands(),
    ];

    public void EditSelectedChart() => charts.EditSelectedChart();
}
