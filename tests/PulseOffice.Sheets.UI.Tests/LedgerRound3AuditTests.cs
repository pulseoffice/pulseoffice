using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerRound3AuditTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static SheetTarget Target(Workbook workbook, int index, UndoStack history, Dictionary<Worksheet, IReadOnlySet<int>?> filtered) => new()
    {
        Workbook = workbook,
        SheetIndex = index,
        History = history,
        Engine = Engines.Create(workbook),
        Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
        WriteSelection = _ => { },
        ReadViewOption = _ => false,
        WriteViewOption = (_, _) => { },
        Refresh = () => { },
        FreezePanes = (_, _) => { },
        SplitPanes = (_, _) => { },
        ReadPanesSplit = () => false,
        ApplyRowFilter = (sheet, rows) => filtered[sheet] = rows,
        ReadZoom = () => 1,
        WriteZoom = _ => { },
        ReadSelectedObject = () => null,
        ReadSelectedObjects = () => [],
        WriteSelectedObjects = _ => { },
        SelectSheet = _ => { },
        WriteSelectedObject = _ => { },
    };

    [Fact]
    public async Task UndoingAFilterFromAnotherSheetHidesRowsOnTheFilteredSheetOnly()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Other");
        var history = new UndoStack();
        var filtered = new Dictionary<Worksheet, IReadOnlySet<int>?>();
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, 0, history, filtered));
        var first = workbook.Sheets[0];
        for (var row = 1; row <= 3; row++) first[new CellAddress(row, 0)] = new Cell { Value = Values.CellValue.Number(row) };
        first.AutoFilter.Activate(new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)));
        first.AutoFilter.Set(new ColumnFilter(0) { Criteria = ">2" });
        var commands = new LedgerFilterCommands(
            context, new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())), Fake<Engine.Operations.ISheetRegions>(), Fake<PulseOffice.UI.Controls.Services.IDialogService>(),
            Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English),
            null!, Fake<DataDialogs.IFilterColumnFacts>(), Fake<DataDialogs.IFilterCriteriaCodec>(), Fake<Engine.Operations.ISheetSort>(), Fake<ISheetSortGate>());
        await commands.GetCommands().Single(c => c.Id == "ledger.data.filterClear").Execute(null!, CancellationToken.None);

        context.Attach(Target(workbook, 1, history, filtered));
        filtered.Clear();
        history.Undo();

        Assert.False(filtered.ContainsKey(workbook.Sheets[1]));
        Assert.NotNull(filtered[first]);
        Assert.NotEmpty(filtered[first]!);
    }
}
