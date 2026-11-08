using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Charts;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class ChartSeriesSourcesCommandTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.TextCatalog Texts = new(PulseOffice.Localization.UiLanguage.English);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private sealed class Scene
    {
        public Scene(int sheetIndex)
        {
            Workbook = Workbook.CreateDefault();
            Other = Workbook.AddSheet("Altro");
            for (var row = 0; row < 3; row++) Other[new CellAddress(row, 1)] = new Cell { Value = CellValue.Number(row + 1) };
            Placed = new PlacedObject
            {
                Id = "g",
                Anchor = new CellAddress(0, 3),
                Content = new ChartDefinition
                {
                    Id = "g",
                    Type = ChartType.Column,
                    SourceSheet = Workbook.Sheets[0].Name,
                    SourceRange = "A1:A3",
                    SeriesSources = [new ChartSeriesSource(null, "Altro!B1:B3", null)],
                },
            };
            Workbook.Sheets[0].Objects.Add(Placed);
            Context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = sheetIndex,
                History = History,
                Engine = Engines.Create(Workbook),
                Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)),
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
                ReadSelectedObject = () => Placed,
                ReadSelectedObjects = () => [Placed],
                WriteSelectedObjects = _ => { },
                SelectSheet = _ => { },
                WriteSelectedObject = _ => { },
            });
        }

        public Workbook Workbook { get; }

        public Worksheet Other { get; }

        public PlacedObject Placed { get; }

        public UndoStack History { get; } = new();

        public ActiveSheetContext Context { get; } = new();

        public string? Values => ((ChartDefinition)Workbook.Sheets[0].Objects[0].Content).SeriesSources![0].Values;
    }

    private sealed class CapturingEditor : IChartEditor
    {
        public ChartEditSource? Source { get; private set; }

        public Task<ChartDefinition?> EditAsync(ChartDefinition chart, ChartEditSource source, CancellationToken ct = default)
        {
            Source = source;
            return Task.FromResult<ChartDefinition?>(null);
        }
    }

    [Fact]
    public async Task InserireRigheSulFoglioDeiDatiSiAnnullaInUnPassoAncheNelGraficoAltrove()
    {
        var scene = new Scene(1);
        var commands = new LedgerStructureCommands(scene.Context, new Engine.Operations.SheetStructure(), new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()),
            new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())), Texts, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>());

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.sheet.insertRowsAbove").Execute(null!, CancellationToken.None);

        Assert.Equal("Altro!B3:B5", scene.Values);

        scene.History.Undo();

        Assert.Equal("Altro!B1:B3", scene.Values);
    }

    [Fact]
    public async Task Inserted_rows_move_names_and_frozen_panes_in_one_undo_step()
    {
        var scene = new Scene(0);
        var sheet = scene.Workbook.Sheets[0];
        scene.Workbook.DefineName(new DefinedName("Data", $"{sheet.Name}.$A$1:$A$3"));
        sheet.Frozen = new FrozenPanes(1, 0);
        var commands = new LedgerStructureCommands(scene.Context, new Engine.Operations.SheetStructure(), new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()),
            new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())), Texts, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>());

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.sheet.insertRowsAbove").Execute(null!, CancellationToken.None);

        Assert.Equal($"{sheet.Name}.$A$3:$A$5", scene.Workbook.FindName("Data")!.Expression);
        Assert.Equal(3, sheet.Frozen.Rows);

        scene.History.Undo();

        Assert.Equal($"{sheet.Name}.$A$1:$A$3", scene.Workbook.FindName("Data")!.Expression);
        Assert.Equal(1, sheet.Frozen.Rows);

        scene.History.Redo();

        Assert.Equal($"{sheet.Name}.$A$3:$A$5", scene.Workbook.FindName("Data")!.Expression);
    }

    [Fact]
    public async Task Deleting_a_filtered_column_renumbers_the_autofilter_in_one_undo_step()
    {
        var scene = new Scene(0);
        var sheet = scene.Workbook.Sheets[0];
        var area = new CellRange(new CellAddress(0, 0), new CellAddress(5, 2));
        sheet.AutoFilter.Activate(area);
        sheet.AutoFilter.Set(new ColumnFilter(0) { Criteria = "North" });
        sheet.AutoFilter.Set(new ColumnFilter(2) { Criteria = ">15" });
        var rangeFilters = new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService()));
        sheet.RangeFilter = new SheetRangeFilter(new CellRange(new CellAddress(10, 0), new CellAddress(14, 1)), true,
            new RangeFilter { Condition = new FilterTerm(1, "a") });
        var commands = new LedgerStructureCommands(scene.Context, new Engine.Operations.SheetStructure(), new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()),
            rangeFilters, Texts, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>());

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.sheet.deleteColumns").Execute(null!, CancellationToken.None);

        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(5, 1)), sheet.AutoFilter.Range);
        Assert.Equal(1, Assert.Single(sheet.AutoFilter.Filters).ColumnOffset);
        Assert.Equal(new FilterTerm(0, "a"), sheet.RangeFilter!.Filter.Condition);
        Assert.NotNull(rangeFilters.Stored(scene.Workbook, 0, new CellRange(new CellAddress(11, 0), new CellAddress(11, 0))));

        scene.History.Undo();

        Assert.Equal(area, sheet.AutoFilter.Range);
        Assert.Equal(2, sheet.AutoFilter.Filters.Count);
        Assert.Equal(new FilterTerm(1, "a"), sheet.RangeFilter!.Filter.Condition);

        scene.History.Redo();

        Assert.Equal(1, Assert.Single(sheet.AutoFilter.Filters).ColumnOffset);
    }

    [Fact]
    public async Task Duplicating_a_sheet_copies_local_names_and_rebased_charts_in_one_undo_step()
    {
        var scene = new Scene(0);
        var source = scene.Workbook.Sheets[0];
        scene.Workbook.DefineName(new DefinedName("Local", $"{source.Name}.$A$1", source.Name));
        var commands = new LedgerSheetCommands(scene.Context, Fake<PulseOffice.UI.Controls.Services.IDialogService>(), Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(), Texts,
            new Engine.Operations.SheetDuplicator(), Fake<Engine.Operations.ISheetRenaming>(), new Engine.Operations.ChartSheetReferences(),
            new WorksheetTabPolicy(), new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
            Fake<PulseOffice.UI.Commands.IRibbonPaletteBuilder>());

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.worksheet.duplicate").Execute(null!, CancellationToken.None);

        var copy = scene.Workbook.Sheets[1];
        Assert.Equal($"{copy.Name}.$A$1", scene.Workbook.FindName("Local", copy.Name)!.Expression);
        Assert.Equal(copy.Name, Assert.IsType<ChartDefinition>(copy.Objects[0].Content).SourceSheet);
        Assert.Equal(source.Name, Assert.IsType<ChartDefinition>(source.Objects[0].Content).SourceSheet);

        scene.History.Undo();
        Assert.DoesNotContain(copy, scene.Workbook.Sheets);
        Assert.Null(scene.Workbook.FindName("Local", copy.Name));

        scene.History.Redo();
        Assert.Equal($"{copy.Name}.$A$1", scene.Workbook.FindName("Local", copy.Name)!.Expression);
    }

    [Fact]
    public async Task EliminareIlFoglioDeiDatiRendeRefLaSerieEAnnullareLaRipristina()
    {
        var scene = new Scene(1);
        var commands = new LedgerSheetCommands(scene.Context, Fake<PulseOffice.UI.Controls.Services.IDialogService>(), Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(), Texts,
            Fake<Engine.Operations.ISheetDuplicator>(), Fake<Engine.Operations.ISheetRenaming>(), new Engine.Operations.ChartSheetReferences(),
            new WorksheetTabPolicy(), new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
            Fake<PulseOffice.UI.Commands.IRibbonPaletteBuilder>());

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.worksheet.delete").Execute(null!, CancellationToken.None);

        Assert.DoesNotContain(scene.Other, scene.Workbook.Sheets);
        Assert.Equal(Engine.Operations.ChartSourceReferences.Broken, scene.Values);

        scene.History.Undo();

        Assert.Contains(scene.Other, scene.Workbook.Sheets);
        Assert.Equal("Altro!B1:B3", scene.Values);
    }

    [Fact]
    public async Task LEditorRiceveGliIntervalliDelleSerieEQualificaQuelliSenzaFoglio()
    {
        var scene = new Scene(0);
        var editor = new CapturingEditor();
        var commands = new LedgerChartCommands(Fake<ISheetFormattingService>(), scene.Context, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            Fake<ISheetImageInsertion>(), Fake<IChartPicker>(), editor, new ChartSettingsMapper(), new Engine.Operations.ChartDataExtractor(),
            Fake<Dialogs.ISheetReferenceSources>(), Texts);

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.chart.edit").Execute(null!, CancellationToken.None);

        var ranges = Assert.IsType<ChartSeriesRangeEditing>(editor.Source?.SeriesRanges);
        Assert.Equal("Altro!C1:C3", ranges.Normalize("altro!C1:C3"));
        Assert.Equal("Sheet1!C1:C3", ranges.Normalize("C1:C3"));
        Assert.Null(ranges.Normalize("Nessuno!C1"));
        Assert.Equal(2, ranges.Expand(new ChartSettingsMapper().From((ChartDefinition)scene.Placed.Content) with { SourceRange = "A1:C3", SeriesSources = null }).Count);
    }
}
