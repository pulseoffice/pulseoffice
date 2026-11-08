using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Commands;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerRound57DialogTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static (Workbook Workbook, ActiveSheetContext Context, PulseOffice.Document.Commands.UndoStack History) Ledger()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Second");
        var history = new PulseOffice.Document.Commands.UndoStack();
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
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
        return (workbook, context, history);
    }

    [Fact]
    public Task Structure_protected_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (workbook, context, _) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));

        var answer = await harness.AnswerAfter(() => workbook.Protection = workbook.Protection with { Structure = true });

        Assert.Null(answer);
        Assert.Contains(harness.Texts["status.dialog.stale.protection"], harness.Reported);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Sheet_protected_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (workbook, context, _) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));

        var answer = await harness.AnswerAfter(() => workbook.Sheets[0].Protection = workbook.Sheets[0].Protection with { IsEnabled = true });

        Assert.Null(answer);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task A_sheet_removed_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (workbook, context, _) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));

        var answer = await harness.AnswerAfter(() => workbook.RemoveSheet(workbook.Sheets[1]));

        Assert.Null(answer);
        Assert.Contains(harness.Texts["status.dialog.stale.target"], harness.Reported);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task The_document_closed_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (_, context, _) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));

        var answer = await harness.AnswerAfter(() => harness.Workspace.Close(harness.Document));

        Assert.Null(answer);
        Assert.Contains(harness.Texts["status.dialog.stale.closed"], harness.Reported);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task The_document_made_read_only_while_the_dialog_is_open_drops_the_answer() => Session.Dispatch(async () =>
    {
        var (_, context, _) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));

        var answer = await harness.AnswerAfter(() => harness.Document.IsReadOnly = true);

        Assert.Null(answer);
        Assert.Contains(harness.Texts["status.dialog.stale.readOnly"], harness.Reported);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task An_untouched_document_keeps_the_answer() => Session.Dispatch(async () =>
    {
        var (_, context, _) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));

        var answer = await harness.AnswerAfter(() => { });

        Assert.Equal("applied", answer);
        Assert.Empty(harness.Reported);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task The_tab_colour_command_applies_nothing_when_the_structure_is_protected_during_its_dialog() => Session.Dispatch(async () =>
    {
        var (workbook, context, history) = Ledger();
        using var harness = new DialogAdmissionHarness(ModuleKind.Sheets, new LedgerStateWitness(context));
        var command = new LedgerSheetCommands(context, harness.Host, harness.Status, harness.Texts,
                Fake<Engine.Operations.ISheetDuplicator>(), Fake<Engine.Operations.ISheetRenaming>(), Fake<Engine.Operations.IChartSheetReferences>(),
                new WorksheetTabPolicy(),
                new DataDialogs.LedgerDialogKit(harness.Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
                Fake<IRibbonPaletteBuilder>())
            .GetCommands().Single(definition => definition.Id.Value == "ledger.worksheet.colour");

        var running = command.Execute(null!, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(harness.Host.IsDialogOpen);
        workbook.Protection = workbook.Protection with { Structure = true };
        harness.Host.Close(new Dictionary<string, object> { ["colour"] = "#FF0000" });
        Dispatcher.UIThread.RunJobs();
        await running;

        Assert.Null(workbook.Sheets[0].TabColorHex);
        Assert.False(history.CanUndo);
        Assert.Contains(harness.Texts["status.dialog.stale.protection"], harness.Reported);
    }, TestContext.Current.CancellationToken);
}
