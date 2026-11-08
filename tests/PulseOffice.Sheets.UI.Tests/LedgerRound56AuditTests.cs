using System.Globalization;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.Conditional;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerRound56AuditTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private sealed class ProtectingDialogs(Workbook workbook, object answer) : IDialogService
    {
        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            workbook.Protection = workbook.Protection with { Structure = true };
            return Task.FromResult((TResult?)answer);
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static (Workbook Workbook, PulseOffice.Document.Commands.UndoStack History, ActiveSheetContext Context) Scene()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Hidden").IsHidden = true;
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
        return (workbook, history, context);
    }

    private static CommandDefinition SheetCommand(ActiveSheetContext context, Workbook workbook, object answer, string id) =>
        new LedgerSheetCommands(context, new ProtectingDialogs(workbook, answer), Fake<IStatusBarService>(), Texts,
                Fake<Engine.Operations.ISheetDuplicator>(), Fake<Engine.Operations.ISheetRenaming>(), Fake<Engine.Operations.IChartSheetReferences>(),
                new WorksheetTabPolicy(),
                new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
                Fake<IRibbonPaletteBuilder>())
            .GetCommands().Single(command => command.Id.Value == id);

    [Fact]
    public Task IlColoreDellaSchedaNonPassaSeLaStrutturaVieneProtettaDuranteIlDialogo() => Session.Dispatch(async () =>
    {
        var (workbook, history, context) = Scene();

        await SheetCommand(context, workbook, new Dictionary<string, object> { ["colour"] = "#FF0000" }, "ledger.worksheet.colour").Execute(null!, CancellationToken.None);

        Assert.Null(workbook.Sheets[0].TabColorHex);
        Assert.False(history.CanUndo);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MostrareIFogliNascostiNonPassaSeLaStrutturaVieneProtettaDuranteIlDialogo() => Session.Dispatch(async () =>
    {
        var (workbook, history, context) = Scene();
        IReadOnlyList<string> chosen = ["Hidden"];

        await SheetCommand(context, workbook, new Dictionary<string, object> { ["sheets"] = chosen }, "ledger.worksheet.show").Execute(null!, CancellationToken.None);

        Assert.True(workbook.FindSheet("Hidden")!.IsHidden);
        Assert.False(history.CanUndo);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void UnConteggioInfinitoDeiPrimiNonCompletaLaRegola()
    {
        var catalog = new ConditionalRuleCatalog();
        var completion = new ConditionalRuleCompletion(catalog, new FormulaLocalization(new FormulaParser(), new FunctionNameVocabulary()));
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        var rule = catalog.Default(ConditionalRuleKind.TopBottom, range);

        Assert.Null(completion.Complete(new ConditionalRuleDraft(rule, range.ToString(), ["1e400"], string.Empty), [range], CultureInfo.InvariantCulture, "en"));
    }
}
