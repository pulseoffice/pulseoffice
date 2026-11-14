using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerViewToggleTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private sealed class Options : IShellViewOptions
    {
        public bool FormulaBarVisible { get; set; } = true;

        public bool SheetTabsVisible { get; set; } = true;

        public bool HeadersVisible { get; set; } = true;
    }

    private static ActiveSheetContext Context()
    {
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = Engines.Create(workbook),
            Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        return context;
    }

    [Theory]
    [InlineData("ledger.view.formulaBar")]
    [InlineData("ledger.view.sheetTabs")]
    [InlineData("ledger.view.headers")]
    public async Task A_view_toggle_shows_whether_its_part_is_visible(string id)
    {
        var commands = new LedgerShellViewCommands(Context(), new Options());
        var toggle = commands.GetCommands().Single(command => command.Id.Value == id);

        Assert.True(toggle.CheckedWhen?.Invoke(CommandContext.Empty));
        await toggle.Execute(CommandContext.Empty, CancellationToken.None);
        Assert.False(toggle.CheckedWhen?.Invoke(CommandContext.Empty));
    }
}
