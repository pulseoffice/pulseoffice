using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Data;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Queries;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class QueryRefreshRunnerTests
{
    [Fact]
    public async Task UnaModificaFattaDuranteLAggiornamentoNonFinisceNelPassoDellAggiornamento()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = "q",
            DisplayName = "q",
            ConnectionId = "c",
            Query = new QueryDefinition { Sql = "SELECT 1" },
            Anchor = new CellAddress(0, 1),
        });
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        var history = new UndoStack();
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = engine,
            Selection = () => CellRange.Single(CellAddress.First),
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
        var refresher = new SlowRefresher();
        var status = new PulseOffice.UI.Controls.Services.StatusBarService(
            TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
            new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
        var runner = new QueryRefreshRunner(context, refresher, new WorkbookCheckpoints(new SheetSnapshots()), status,
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        var running = runner.RefreshAsync(CancellationToken.None);
        var typed = new SheetCell(0, CellAddress.First);
        engine.SetValue(typed, CellValue.Number(7));
        history.Push(new DelegateAction("Type", () => engine.SetValue(typed, CellValue.Number(7)), () => engine.SetValue(typed, CellValue.Empty)));
        refresher.Release();
        await running;

        Assert.Equal(42, sheet[new CellAddress(0, 1)].Value.AsNumber());
        history.Undo();

        Assert.True(sheet[new CellAddress(0, 1)].Value.IsEmpty);
        Assert.Equal(7, sheet[CellAddress.First].Value.AsNumber());
    }

    private sealed class SlowRefresher : IQueryRefresher
    {
        private readonly TaskCompletionSource _gate = new();

        public void Release() => _gate.SetResult();

        public Task<OperationResult<QueryBinding>> RefreshAsync(Workbook workbook, Worksheet sheet, QueryBinding binding, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task<OperationResult<int>> RefreshAllAsync(Workbook workbook, CancellationToken ct = default) =>
            Apply(workbook, await FetchAllAsync(workbook, ct));

        public async Task<QueryFetch> FetchAllAsync(Workbook workbook, CancellationToken ct = default)
        {
            await _gate.Task;
            return new QueryFetch([]);
        }

        public OperationResult<int> Apply(Workbook workbook, QueryFetch fetch)
        {
            workbook.Sheets[0][new CellAddress(0, 1)] = new Cell { Value = CellValue.Number(42) };
            return OperationResult<int>.Ok(1);
        }
    }
}
