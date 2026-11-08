using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerCommandAuditTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private sealed record Harness(Workbook Workbook, ActiveSheetContext Context, UndoStack History, Engine.Recalculation.CalculationEngine Engine, TextCatalog Texts, StatusBarService Status);

    private static Harness Build(CellRange selection)
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
        var engine = Engines.Create(workbook);
        var context = new ActiveSheetContext();
        var zoom = 1.0;
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = engine,
            Selection = () => selection,
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => zoom,
            WriteZoom = value => zoom = value,
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        var status = new StatusBarService(
            TimeProvider.System, new NotificationCenter(TimeProvider.System), new NotificationHistory(TimeProvider.System));
        return new Harness(workbook, context, history, engine, new TextCatalog(UiLanguage.English), status);
    }

    private static Task Run(IEnumerable<CommandDefinition> commands, string id) =>
        commands.Single(command => command.Id.Value == id).Execute(
            new CommandContext(Foundation.Modules.ModuleKind.Sheets, SelectionKind.Cell, DocumentState.DocumentOpen), CancellationToken.None);

    [Fact]
    public async Task IlCampoDataInserisceLaFormulaCanonicaEdAnnullaRestituisceLaFormulaPrecedente()
    {
        var harness = Build(CellRange.Single(A("B2")));
        harness.Engine.SetFormula(new SheetCell(0, A("B2")), "=1+1");
        var commands = new LedgerMiscCommands(harness.Context, null!, harness.Texts);

        await Run(commands.GetCommands(), "ledger.insert.fieldDate");

        var sheet = harness.Workbook.Sheets[0];
        Assert.Equal("=TODAY()", sheet[A("B2")].Formula);
        Assert.Equal(ValueKind.Number, harness.Engine.GetValue(new SheetCell(0, A("B2"))).Kind);

        harness.History.Undo();
        Assert.Equal("=1+1", sheet[A("B2")].Formula);
    }

    [Fact]
    public async Task UnSimboloSuUnaFormulaSiAnnullaRestituendoLaFormula()
    {
        var harness = Build(CellRange.Single(A("C3")));
        harness.Engine.SetFormula(new SheetCell(0, A("C3")), "=\"ab\"");
        var commands = new LedgerMiscCommands(harness.Context, null!, harness.Texts);

        await Run(commands.GetCommands(), "ledger.insert.nonBreakingHyphen");

        var sheet = harness.Workbook.Sheets[0];
        Assert.False(sheet[A("C3")].HasFormula);
        harness.History.Undo();
        Assert.Equal("=\"ab\"", sheet[A("C3")].Formula);
    }

    [Fact]
    public async Task AnnullareIlNomeDelFoglioRicalcolaLeCelleDipendenti()
    {
        var harness = Build(CellRange.Single(A("A1")));
        harness.Engine.SetFormula(new SheetCell(0, A("B1")), "=LEN(A1)");
        var commands = new LedgerInsertCommands(harness.Context, null!, harness.Texts);

        await Run(commands.GetCommands(), "ledger.insert.sheetName");
        var length = harness.Workbook.Sheets[0].Name.Length;
        Assert.Equal(length, harness.Engine.GetValue(new SheetCell(0, A("B1"))).AsNumber());

        harness.History.Undo();
        Assert.Equal(0, harness.Engine.GetValue(new SheetCell(0, A("B1"))).AsNumber());

        harness.History.Redo();
        Assert.Equal(length, harness.Engine.GetValue(new SheetCell(0, A("B1"))).AsNumber());
    }

    [Fact]
    public async Task CancellareIFiltriLiTogleDalModelloEAnnullareLiRimette()
    {
        var harness = Build(new CellRange(A("A1"), A("A4")));
        var sheet = harness.Workbook.Sheets[0];
        sheet.AutoFilter.Activate(new CellRange(A("A1"), A("A4")));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string> { "x" } });
        var commands = new LedgerFilterCommands(harness.Context, new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())), null!, null!,
            harness.Status, harness.Texts, null!, null!, null!, null!, null!);

        await Run(commands.GetCommands(), "ledger.data.filterClear");
        Assert.Empty(sheet.AutoFilter.Filters);
        Assert.True(sheet.AutoFilter.IsActive);

        harness.History.Undo();
        Assert.Single(sheet.AutoFilter.Filters);
    }

    [Fact]
    public async Task CancellareIFiltriSuUnFoglioProtettoSenzaPermessoNonFaNulla()
    {
        var harness = Build(new CellRange(A("A1"), A("A4")));
        var sheet = harness.Workbook.Sheets[0];
        sheet.AutoFilter.Activate(new CellRange(A("A1"), A("A4")));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string> { "x" } });
        sheet.Protection = new SheetProtection { IsEnabled = true };
        var commands = new LedgerFilterCommands(harness.Context, new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())), null!, null!,
            harness.Status, harness.Texts, null!, null!, null!, null!, null!);

        await Run(commands.GetCommands(), "ledger.data.filterClear");
        Assert.Single(sheet.AutoFilter.Filters);
    }
}
