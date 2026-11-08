using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerCommandsRound6Tests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private sealed class AnsweringDialogs(object answer) : IDialogService
    {
        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) =>
            Task.FromResult((TResult?)answer);

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private sealed class Scene
    {
        public Workbook Workbook { get; } = Workbook.CreateDefault();
        public UndoStack History { get; } = new();
        public ActiveSheetContext Context { get; } = new();
        public Engine.Recalculation.CalculationEngine Engine { get; }

        public Scene()
        {
            Engine = Engines.Create(Workbook);
            Context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = 0,
                History = History,
                Engine = Engine,
                Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
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
        }

        public SheetCell At(string a1)
        {
            Assert.True(CellAddress.TryParseA1(a1, out var address));
            return new SheetCell(0, address);
        }

        public double Number(string a1) => Engine.GetValue(At(a1)).AsNumber();
    }

    private static StatusBarService Status() => new(
        TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
        new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static DataDialogs.LedgerDialogKit Kit() =>
        new(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter());

    private sealed class NoProbe : DataDialogs.IAdvancedFilterProbes, DataDialogs.IAdvancedFilterProbe
    {
        public DataDialogs.IAdvancedFilterProbe For(Workbook workbook, int sheetIndex, Engine.Operations.IShiftedFormulaEvaluator? formulas = null) => this;

        public DataDialogs.AdvancedFilterProbe Probe(DataDialogs.AdvancedFilterChoice choice) => new([], null, 0);
    }

    private sealed class PlainSources : Dialogs.ISheetReferenceSources, PulseOffice.UI.Controls.IReferenceSource
    {
        public bool IsValid(string text) => true;

        public PulseOffice.UI.Controls.References.IReferencePicker? Picker => null;

        public PulseOffice.UI.Controls.IReferenceSource For(Dialogs.ReferenceShape shape) => this;

        PulseOffice.UI.Controls.References.IReferencePicker Dialogs.ISheetReferenceSources.Picker(Dialogs.ReferenceShape shape) => null!;

        public PulseOffice.UI.Controls.IReferenceSource ForExpression() => this;
    }

    private static CommandDefinition AdvancedFilter(Scene scene, DataDialogs.AdvancedFilterChoice answer) =>
        new LedgerDataDialogCommands(scene.Context, Fake<Engine.Operations.ISheetConsolidation>(), Fake<Engine.Operations.IMultipleOperations>(),
                new Engine.Operations.SheetFilterService(), Fake<ISheetValueWrites>(), Fake<Dialogs.ISheetReferenceReader>(),
                new PlainSources(), new AnsweringDialogs(answer), Status(), Texts, Kit(),
                new NoProbe(), Fake<DataDialogs.ILedgerDialogProbes>(), new Engine.Operations.SheetRangeReferences(),
                new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())))
            .GetCommands().Single(command => command.Id.Value == "ledger.data.advancedFilter");

    private static Scene FilterScene()
    {
        var scene = new Scene();
        scene.Engine.SetValue(scene.At("A1"), CellValue.Text("Name"));
        scene.Engine.SetValue(scene.At("B1"), CellValue.Text("Amount"));
        scene.Engine.SetValue(scene.At("A2"), CellValue.Text("a"));
        scene.Engine.SetValue(scene.At("B2"), CellValue.Number(5));
        scene.Engine.SetValue(scene.At("A3"), CellValue.Text("b"));
        scene.Engine.SetValue(scene.At("B3"), CellValue.Number(-2));
        scene.Engine.SetValue(scene.At("A4"), CellValue.Text("c"));
        scene.Engine.SetValue(scene.At("B4"), CellValue.Number(7));
        scene.Engine.SetValue(scene.At("D1"), CellValue.Text("Amount"));
        scene.Engine.SetValue(scene.At("D2"), CellValue.Text(">0"));
        scene.Engine.SetFormula(scene.At("J1"), "=SUM(G1:G10)");
        return scene;
    }

    [Fact]
    public async Task CopiareIRisultatiDelFiltroSpecialeRicalcolaLeFormuleCheLiLeggono()
    {
        var scene = FilterScene();

        await AdvancedFilter(scene, new DataDialogs.AdvancedFilterChoice("A1:B4", "D1:D2", false, "F1")).Execute(null!, CancellationToken.None);

        Assert.Equal(12, scene.Number("G2") + scene.Number("G3"));
        Assert.Equal(12, scene.Number("J1"));
    }

    [Fact]
    public async Task LeFormuleCopiateDalFiltroSpecialeVengonoCalcolate()
    {
        var scene = FilterScene();
        scene.Engine.SetValue(scene.At("C1"), CellValue.Text("Twice"));
        scene.Engine.SetFormula(scene.At("C2"), "=B2*2");
        scene.Engine.SetFormula(scene.At("C3"), "=B3*2");
        scene.Engine.SetFormula(scene.At("C4"), "=B4*2");

        await AdvancedFilter(scene, new DataDialogs.AdvancedFilterChoice("A1:C4", "D1:D2", false, "F1")).Execute(null!, CancellationToken.None);
        scene.Engine.SetValue(scene.At("G3"), CellValue.Number(100));

        Assert.Equal(200, scene.Number("H3"));
    }

    [Fact]
    public async Task AnnullareLaCopiaDelFiltroSpecialeEUnSoloPassoERicalcola()
    {
        var scene = FilterScene();
        await AdvancedFilter(scene, new DataDialogs.AdvancedFilterChoice("A1:B4", "D1:D2", false, "F1")).Execute(null!, CancellationToken.None);

        scene.History.Undo();

        Assert.False(scene.History.CanUndo);

        Assert.True(scene.Workbook.Sheets[0][scene.At("G2").Address].Value.IsEmpty);
        Assert.Equal(0, scene.Number("J1"));
    }

    [Fact]
    public async Task The_advanced_filter_in_place_is_stored_on_the_sheet_and_undone_in_one_step()
    {
        var scene = FilterScene();

        await AdvancedFilter(scene, new DataDialogs.AdvancedFilterChoice("A1:B4", "D1:D2", true, "")).Execute(null!, CancellationToken.None);

        var stored = scene.Workbook.Sheets[0].RangeFilter;
        Assert.NotNull(stored);
        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)), stored.Range);
        Assert.Equal(new AdvancedCriteria(0, new CellRange(new CellAddress(0, 3), new CellAddress(1, 3))), stored.Filter.Criteria);
        Assert.True(stored.Filter.UniqueOnly);

        scene.History.Undo();
        Assert.Null(scene.Workbook.Sheets[0].RangeFilter);
        scene.History.Redo();
        Assert.NotNull(scene.Workbook.Sheets[0].RangeFilter);
    }

    [Fact]
    public async Task The_advanced_filter_in_place_evaluates_computed_criteria()
    {
        var scene = FilterScene();
        scene.Engine.SetFormula(scene.At("E2"), "=B2>6");
        IReadOnlySet<int>? applied = null;
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = scene.Workbook, SheetIndex = 0, History = scene.History, Engine = scene.Engine,
            Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            WriteSelection = _ => { }, ReadViewOption = _ => false, WriteViewOption = (_, _) => { }, Refresh = () => { },
            FreezePanes = (_, _) => { }, SplitPanes = (_, _) => { }, ReadPanesSplit = () => false,
            ApplyRowFilter = (_, hidden) => applied = hidden,
            ReadZoom = () => 1, WriteZoom = _ => { }, ReadSelectedObject = () => null, ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { }, SelectSheet = _ => { }, WriteSelectedObject = _ => { },
        });
        var filters = new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService()));

        filters.Store(context, scene.Workbook.Sheets[0], new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)), true,
            new RangeFilter { Criteria = new AdvancedCriteria(0, new CellRange(new CellAddress(0, 4), new CellAddress(1, 4))) }, "filter");

        Assert.Equal([1, 2], applied!.Order());
    }

    private static CommandDefinition PivotCreate(Scene scene, PivotChoice answer)
    {
        var engine = new Pivot.PivotEngine(new Pivot.PivotAggregator(), new Pivot.PivotGrouping(),
            new Pivot.PivotFormulas(new Engine.Parsing.FormulaParser()));
        return new LedgerPivotCommands(scene.Context, new Pivot.PivotCoordinator(engine, new Pivot.PivotRenderer()), new AnsweringDialogs(answer),
                Status(), Texts, new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()), Kit(), engine,
                new WorksheetTabPolicy())
            .GetCommands().Single(command => command.Id.Value == "ledger.data.pivotCreate");
    }

    [Theory]
    [InlineData("Q1:Q2")]
    [InlineData("a/b")]
    [InlineData("[x]")]
    public async Task LaTabellaPivotNonCreaUnFoglioConUnNomeNonValido(string name)
    {
        var scene = FilterScene();
        var fields = new List<PulseOffice.Sheets.Pivot.PivotField>
        {
            new() { SourceName = "Name", SourceColumnIndex = 0, Area = PulseOffice.Sheets.Pivot.PivotArea.Row },
            new() { SourceName = "Amount", SourceColumnIndex = 1, Area = PulseOffice.Sheets.Pivot.PivotArea.Value },
        };

        await PivotCreate(scene, new PivotChoice(fields, name)).Execute(null!, CancellationToken.None);

        Assert.Single(scene.Workbook.Sheets);
        Assert.Empty(scene.Workbook.Pivots);
    }

    private static CommandDefinition SheetTool(Scene scene, string id) =>
        new LedgerSheetToolsCommands(scene.Context, new Engine.Operations.SheetOutline(), new Engine.Operations.SheetSnapshots(),
                new AnsweringDialogs(null!), Status(), Texts)
            .GetCommands().Single(command => command.Id.Value == id);

    [Theory]
    [InlineData("ledger.comment.deleteAll")]
    [InlineData("ledger.comment.showAll")]
    [InlineData("ledger.outline.clear")]
    [InlineData("ledger.break.insertRow")]
    public async Task GliStrumentiDelFoglioNonCambianoUnFoglioProtetto(string id)
    {
        var scene = new Scene();
        var sheet = scene.Workbook.Sheets[0];
        sheet.Comments[CellAddress.First] = new CellComment { Text = "note" };
        sheet.RowGroups.Add(new OutlineGroup { First = 2, Last = 4 });
        sheet.Protection = new SheetProtection { IsEnabled = true };

        await SheetTool(scene, id).Execute(null!, CancellationToken.None);

        Assert.Single(sheet.Comments);
        Assert.False(sheet.Comments[CellAddress.First].IsAlwaysVisible);
        Assert.Single(sheet.RowGroups);
        Assert.Empty(sheet.RowBreaks);
        Assert.False(scene.History.CanUndo);
    }

    private sealed class CapturingFormatting : ISheetFormattingService
    {
        public Func<PulseOffice.Sheets.Styling.CellStyle, PulseOffice.Sheets.Styling.CellStyle>? Applied { get; private set; }

        public bool CanFormat => true;

        public PulseOffice.Sheets.Styling.CellStyle CurrentStyle => PulseOffice.Sheets.Styling.CellStyle.Default;

        public void ApplyToSelection(Func<PulseOffice.Sheets.Styling.CellStyle, PulseOffice.Sheets.Styling.CellStyle> change) => Applied = change;

        public void ApplyToSelection(string description, Func<PulseOffice.Sheets.Styling.CellStyle, PulseOffice.Sheets.Styling.CellStyle> change) => Applied = change;

        public void ApplyToSelection(string description, Func<CellRange, CellAddress, PulseOffice.Sheets.Styling.CellStyle, PulseOffice.Sheets.Styling.CellStyle> change) =>
            Applied = style => change(CellRange.Single(CellAddress.First), CellAddress.First, style);

        public void ToggleOnSelection(Func<PulseOffice.Sheets.Styling.CellStyle, bool> read, Func<PulseOffice.Sheets.Styling.CellStyle, bool, PulseOffice.Sheets.Styling.CellStyle> write)
        {
        }

        public bool Accepts(string commandId) => true;
    }

    [Fact]
    public async Task IlPennelloPortaIlFormatoNumericoGiustoInUnAltraCartella()
    {
        var source = new Scene();
        var custom = source.Workbook.NumberFormats.Register("0.000");
        source.Workbook.Sheets[0].SetStyle(CellAddress.First, source.Workbook.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { NumberFormatId = custom }));

        var formatting = new CapturingFormatting();
        var commands = new LedgerFinalCommands(source.Context, new AnsweringDialogs(null!), Status(), Texts,
            Fake<PulseOffice.Sheets.Styling.ISheetThemeApplier>(), Fake<PulseOffice.Sheets.Pivot.IPivotFormulas>(), Fake<PulseOffice.Sheets.Pivot.IPivotCoordinator>(),
            Fake<Engine.Operations.IWorkbookCheckpoints>(), formatting, Fake<PulseOffice.Sheets.Formatting.IColumnAutoWidth>(),
            Fake<Dialogs.ISheetReferenceSources>(), Fake<Dialogs.ISheetReferenceReader>(), Fake<PulseOffice.UI.Controls.Tables.ITableStyleGallery>(),
            Fake<ILedgerTableLooks>(), Fake<PulseOffice.UI.Controls.Themes.IThemePaletteGallery>(), null!, Fake<INumberFormatCodeEditor>());
        var paintbrush = commands.GetCommands().Single(command => command.Id.Value == "ledger.format.paintbrush");

        await paintbrush.Execute(null!, CancellationToken.None);

        var target = new Scene();
        target.Workbook.NumberFormats.Register("#,##0.0");
        source.Context.Attach(new SheetTarget
        {
            Workbook = target.Workbook,
            SheetIndex = 0,
            History = target.History,
            Engine = target.Engine,
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

        await paintbrush.Execute(null!, CancellationToken.None);

        var applied = formatting.Applied!(PulseOffice.Sheets.Styling.CellStyle.Default);
        Assert.Equal("0.000", target.Workbook.NumberFormats.CodeOf(applied.NumberFormatId));
    }

    private sealed class FailingPivots(Pivot.IPivotCoordinator inner) : Pivot.IPivotCoordinator
    {
        public int Applied { get; private set; }

        public PulseOffice.Foundation.Diagnostics.OperationResult<CellRange> Apply(Workbook workbook, PulseOffice.Sheets.Pivot.PivotDefinition definition, Pivot.PivotLabels labels)
        {
            Applied++;
            return PulseOffice.Foundation.Diagnostics.OperationResult<CellRange>.Fail(PulseOffice.Foundation.Diagnostics.Diagnostic.Error("PVT006", "full"));
        }

        public PulseOffice.Foundation.Diagnostics.OperationResult<int> RefreshAll(Workbook workbook, Pivot.PivotLabels labels) => inner.RefreshAll(workbook, labels);

        public void Remove(Workbook workbook, string id) => inner.Remove(workbook, id);
    }

    private static (Scene Scene, Pivot.PivotCoordinator Real) PivotScene()
    {
        var scene = FilterScene();
        var engine = new Pivot.PivotEngine(new Pivot.PivotAggregator(), new Pivot.PivotGrouping(), new Pivot.PivotFormulas(new Engine.Parsing.FormulaParser()));
        var real = new Pivot.PivotCoordinator(engine, new Pivot.PivotRenderer());
        var result = real.Apply(scene.Workbook, new PulseOffice.Sheets.Pivot.PivotDefinition
        {
            Id = "p",
            Name = "Pivot1",
            SourceSheet = scene.Workbook.Sheets[0].Name,
            SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            DestinationSheet = "Pivot1",
            Destination = CellAddress.First,
            Fields =
            [
                new() { SourceName = "Name", SourceColumnIndex = 0, Area = PulseOffice.Sheets.Pivot.PivotArea.Row },
                new() { SourceName = "Amount", SourceColumnIndex = 1, Area = PulseOffice.Sheets.Pivot.PivotArea.Value },
            ],
        }, new Pivot.PivotLabels("Total", "Subtotal"));
        Assert.True(result.Succeeded);
        return (scene, real);
    }

    private static CommandDefinition CalculatedField(Scene scene, Pivot.IPivotCoordinator pivots) =>
        new LedgerFinalCommands(scene.Context,
                new AnsweringDialogs(new Dictionary<string, object> { ["name"] = "Doubled", ["formula"] = "=Amount*2" }),
                Status(), Texts, Fake<PulseOffice.Sheets.Styling.ISheetThemeApplier>(),
                new Pivot.PivotFormulas(new Engine.Parsing.FormulaParser()), pivots,
                new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()), new CapturingFormatting(),
                Fake<PulseOffice.Sheets.Formatting.IColumnAutoWidth>(), new PlainSources(), Fake<Dialogs.ISheetReferenceReader>(),
                Fake<PulseOffice.UI.Controls.Tables.ITableStyleGallery>(), Fake<ILedgerTableLooks>(),
                Fake<PulseOffice.UI.Controls.Themes.IThemePaletteGallery>(), null!, Fake<INumberFormatCodeEditor>())
            .GetCommands().Single(command => command.Id.Value == "ledger.pivot.calculatedField");

    [Fact]
    public async Task UnCampoCalcolatoNonScriveInUnaPivotSuUnFoglioProtetto()
    {
        var (scene, real) = PivotScene();
        var destination = scene.Workbook.FindSheet("Pivot1")!;
        destination.Protection = new SheetProtection { IsEnabled = true };

        await CalculatedField(scene, real).Execute(null!, CancellationToken.None);

        Assert.Equal(2, scene.Workbook.Pivots[0].Fields.Count);
        Assert.False(scene.History.CanUndo);
    }

    [Fact]
    public async Task UnCampoCalcolatoFallitoNonLasciaUnPassoVuotoNellaCronologia()
    {
        var (scene, real) = PivotScene();
        var failing = new FailingPivots(real);

        await CalculatedField(scene, failing).Execute(null!, CancellationToken.None);

        Assert.Equal(1, failing.Applied);
        Assert.False(scene.History.CanUndo);
    }
}
