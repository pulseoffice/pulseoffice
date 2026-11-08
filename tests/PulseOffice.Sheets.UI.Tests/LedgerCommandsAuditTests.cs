using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerCommandsAuditTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private sealed class CapturingFormatting : ISheetFormattingService
    {
        public CellStyle Style { get; set; } = CellStyle.Default;
        public bool CanFormat => true;
        public CellStyle CurrentStyle => Style;
        public bool Accepts(string commandId) => true;
        public void ApplyToSelection(Func<CellStyle, CellStyle> change) => Style = change(Style);
        public void ApplyToSelection(string description, Func<CellStyle, CellStyle> change) => Style = change(Style);
        public void ApplyToSelection(string description, Func<CellRange, CellAddress, CellStyle, CellStyle> change) { }
        public void ToggleOnSelection(Func<CellStyle, bool> read, Func<CellStyle, bool, CellStyle> write) { }
    }

    private static string ChangeDecimals(string code, string commandId)
    {
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        var zoom = 1.0;
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
            ReadZoom = () => zoom,
            WriteZoom = value => zoom = value,
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });

        var formatting = new CapturingFormatting();
        formatting.Style = formatting.Style with { NumberFormatId = workbook.NumberFormats.Register(code) };

        var command = new LedgerCellFormatCommands(formatting, context, new NumberFormatCodeEditor(), new Formatting.SystemDateCode()).GetCommands().Single(c => c.Id == commandId);
        command.Execute(null!, CancellationToken.None).GetAwaiter().GetResult();

        return workbook.NumberFormats.Get(formatting.Style.NumberFormatId).Code;
    }

    [Theory]
    [InlineData("0.00%", "ledger.number.moreDecimals", "0.000%")]
    [InlineData("0.00%", "ledger.number.fewerDecimals", "0.0%")]
    [InlineData("#,##0.00 \"€\"", "ledger.number.moreDecimals", "#,##0.000 \"€\"")]
    [InlineData("0.00E+00", "ledger.number.fewerDecimals", "0.0E+00")]
    [InlineData("0.0;[Red]-0.0", "ledger.number.moreDecimals", "0.00;[Red]-0.00")]
    [InlineData("dd/mm/yyyy", "ledger.number.moreDecimals", "dd/mm/yyyy")]
    [InlineData("0", "ledger.number.moreDecimals", "0.0")]
    public void IDecimaliCambianoSenzaPerdereIlRestoDelFormato(string code, string command, string expected) =>
        Assert.Equal(expected, ChangeDecimals(code, command));

    public class Inert : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType switch
            {
                { IsValueType: true } type when type != typeof(void) => Activator.CreateInstance(type),
                { IsGenericType: true } type when Record(targetMethod, args) && type.GetGenericTypeDefinition() == typeof(Task<>) =>
                    typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GenericTypeArguments[0]).Invoke(null, [null]),
                { } type when type == typeof(Task) => Task.CompletedTask,
                _ => Calls(targetMethod, args),
            };

        public List<(string Method, object?[] Arguments)> Log { get; } = [];

        private bool Record(System.Reflection.MethodInfo method, object?[]? args)
        {
            lock (Log) Log.Add((method.Name, args ?? []));
            return true;
        }

        private object? Calls(System.Reflection.MethodInfo? method, object?[]? args)
        {
            lock (Log) Log.Add((method?.Name ?? string.Empty, args ?? []));
            return null;
        }
    }

    [Fact]
    public async Task RipetereLoSpegnimentoNonScavalcaLaProtezioneDelleModifiche()
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
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
        workbook.Changes.IsRecording = true;
        var commands = new LedgerChangeCommands(
            context,
            System.Reflection.DispatchProxy.Create<PulseOffice.UI.Controls.ISidePanelHost, Inert>(),
            System.Reflection.DispatchProxy.Create<PulseOffice.UI.Controls.Services.IStatusBarService, Inert>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));
        var record = commands.GetCommands().Single(c => c.Id == "ledger.changes.record");

        await record.Execute(null!, CancellationToken.None);
        workbook.Changes.IsProtected = true;
        history.Undo();
        history.Redo();

        Assert.False(history.CanUndo);
        Assert.False(workbook.Changes.IsRecording);
    }

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, Inert>();

    [Fact]
    public async Task EliminaIlGraficoNonToglieImmaginiEFormeNellaSelezione()
    {
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = Engines.Create(workbook),
            Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(5, 5)),
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
        var sheet = workbook.Sheets[0];
        sheet.Objects.Add(new PlacedObject { Id = "chart", Content = new PulseOffice.Charts.ChartDefinition { Id = "chart", Type = PulseOffice.Charts.ChartType.Column, SourceSheet = "Sheet1", SourceRange = "A1:B2" }, Anchor = new CellAddress(1, 1) });
        sheet.Objects.Add(new PlacedObject { Id = "note", Content = new PulseOffice.Document.Embedding.DataTableObject { Id = "note", Query = new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = "c", Sql = "s" } }, Anchor = new CellAddress(2, 2) });
        var commands = new LedgerChartCommands(
            Fake<ISheetFormattingService>(), context, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            Fake<ISheetImageInsertion>(), Fake<PulseOffice.UI.Controls.Charts.IChartPicker>(),
            Fake<PulseOffice.UI.Controls.Charts.IChartEditor>(), Fake<PulseOffice.Charts.IChartSettingsMapper>(),
            Fake<PulseOffice.Sheets.Engine.Operations.IChartDataExtractor>(), Fake<Dialogs.ISheetReferenceSources>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        await commands.GetCommands().Single(c => c.Id == "ledger.chart.delete").Execute(null!, CancellationToken.None);

        Assert.Equal(["note"], sheet.Objects.Select(placed => placed.Id));
    }

    [Theory]
    [InlineData("image/tiff", ".tif")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/png", ".png")]
    public async Task EsportaImmagineProponeLEstensioneDelSuoFormato(string mediaType, string extension)
    {
        var workbook = Workbook.CreateDefault();
        var image = new PlacedObject
        {
            Id = "img",
            Content = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "img", Data = new byte[] { 1, 2, 3 }, MediaType = mediaType },
            Anchor = new CellAddress(0, 0),
        };
        workbook.Sheets[0].Objects.Add(image);
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
            ReadSelectedObject = () => image,
            ReadSelectedObjects = () => [image],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        var files = Fake<PulseOffice.Platform.IFileDialogService>();
        var commands = new LedgerDocumentExtraCommands(
            context, files, Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English),
            Fake<PulseOffice.UI.Controls.Objects.IImageLinks>(), Fake<Queries.IQueryRefresher>(),
            Fake<PulseOffice.Sheets.Engine.Operations.ISheetSnapshots>(), Fake<PulseOffice.Platform.IAtomicFileWriter>(),
            new PulseOffice.Drawing.Assets.RasterMediaTypes());
        var export = commands.GetCommands().Single(c => c.Id == "ledger.image.export");

        await export.Execute(null!, CancellationToken.None);

        string suggested;
        var log = ((Inert)(object)files).Log;
        lock (log)
            suggested = (string)log.Last(call => call.Method == "SaveFileAsync").Arguments[1]!;
        Assert.EndsWith(extension, suggested, StringComparison.Ordinal);
        Assert.Equal(PulseOffice.UI.Commands.DocumentState.None, export.ForbiddenState);
    }

    [Fact]
    public async Task CancellaFiltroToglieLeCondizioniEdEAnnullabile()
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
        IReadOnlySet<int>? hidden = new HashSet<int> { 2 };
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
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
            ApplyRowFilter = (_, rows) => hidden = rows,
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { Criteria = ">1" });
        var commands = new LedgerFilterCommands(
            context, new SheetRangeFilters(new Engine.Operations.RangeFilterEvaluator(new Engine.Operations.SheetFilterService())), Fake<Engine.Operations.ISheetRegions>(), Fake<PulseOffice.UI.Controls.Services.IDialogService>(),
            Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English),
            null!, Fake<DataDialogs.IFilterColumnFacts>(), Fake<DataDialogs.IFilterCriteriaCodec>(), Fake<Engine.Operations.ISheetSort>(), Fake<ISheetSortGate>());

        await commands.GetCommands().Single(c => c.Id == "ledger.data.filterClear").Execute(null!, CancellationToken.None);

        Assert.Empty(sheet.AutoFilter.Filters);
        Assert.Null(hidden);
        history.Undo();
        Assert.Single(sheet.AutoFilter.Filters);
    }

    [Theory]
    [InlineData("0", true, "#,##0")]
    [InlineData("0.00%", true, "#,##0.00%")]
    [InlineData("#,##0.00 \"€\"", false, "0.00 \"€\"")]
    [InlineData("0.0;[Red]-0.0", true, "#,##0.0;[Red]-#,##0.0")]
    [InlineData("#,##0,", false, "0,")]
    [InlineData("0000", true, "0,000")]
    public void IlSeparatoreDelleMigliaiaNonCambiaIlRestoDelFormato(string code, bool grouped, string expected) =>
        Assert.Equal(expected, new NumberFormatCodeEditor().WithGrouping(code, grouped));

    [Theory]
    [InlineData("#,##0.00", true)]
    [InlineData("0.00", false)]
    [InlineData("0,", false)]
    public void RiconosceIlSeparatoreDelleMigliaia(string code, bool expected) =>
        Assert.Equal(expected, new NumberFormatCodeEditor().HasGrouping(code));

    [Fact]
    public async Task InserireUnCampoSuUnaFormulaPassaDalMotoreAncheAnnullando()
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
        var engine = Engines.Create(workbook);
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = engine,
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
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=1+1");
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 1)), "=A1*2");
        var commands = new LedgerInsertCommands(context, Fake<PulseOffice.UI.Controls.Links.IHyperlinkPrompt>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        await commands.GetCommands().Single(c => c.Id == "ledger.insert.currentTime").Execute(null!, CancellationToken.None);
        var sheet = workbook.Sheets[0];
        var time = sheet.GetValue(new CellAddress(0, 0)).AsNumber();
        Assert.False(sheet[new CellAddress(0, 0)].HasFormula);
        Assert.Equal(time * 2, sheet.GetValue(new CellAddress(0, 1)).AsNumber(), 9);

        history.Undo();
        Assert.Equal(4, sheet.GetValue(new CellAddress(0, 1)).AsNumber());
    }

    private static (Workbook Workbook, ActiveSheetContext Context, UndoStack History, Engine.Recalculation.CalculationEngine Engine) Sheet()
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
        var engine = Engines.Create(workbook);
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = engine,
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
        return (workbook, context, history, engine);
    }

    private static LedgerMiscCommands Misc(ActiveSheetContext context) => new(
        context, Fake<PulseOffice.UI.Controls.Services.IDialogService>(),
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

    [Fact]
    public async Task IlCampoDataScriveUnaFormulaCanonicaCheSiCalcola()
    {
        var (workbook, context, _, _) = Sheet();

        await Misc(context).GetCommands().Single(c => c.Id == "ledger.insert.fieldDate").Execute(null!, CancellationToken.None);

        Assert.Equal(Values.ValueKind.Number, workbook.Sheets[0].GetValue(new CellAddress(0, 0)).Kind);
    }

    [Fact]
    public async Task AnnullareUnSimboloInseritoRestituisceLaFormula()
    {
        var (workbook, context, history, engine) = Sheet();
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=1+1");

        await Misc(context).GetCommands().Single(c => c.Id == "ledger.insert.nonBreakingSpace").Execute(null!, CancellationToken.None);
        history.Undo();

        Assert.True(workbook.Sheets[0][new CellAddress(0, 0)].HasFormula);
    }

    [Fact]
    public async Task SelezionaRigheVisibiliGuardaLaRigaGiustaNonLaSuccessiva()
    {
        var workbook = Workbook.CreateDefault();
        var selection = new CellRange(new CellAddress(0, 0), new CellAddress(2, 0));
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = Engines.Create(workbook),
            Selection = () => selection,
            WriteSelection = range => selection = range,
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
        workbook.Sheets[0].Layout.HideRow(2);
        var commands = new LedgerSelectionExtraCommands(context, new SheetSelectionSet(),
            Fake<PulseOffice.UI.Controls.Services.IDialogService>(), Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        await commands.GetCommands().Single(c => c.Id == "ledger.select.visibleRows").Execute(null!, CancellationToken.None);

        Assert.Equal(1, selection.End.Row);
    }

    [Fact]
    public async Task IlDizionarioDeiSinonimiNonSostituisceUnaFormula()
    {
        var workbook = Workbook.CreateDefault();
        var engine = Engines.Create(workbook);
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = engine,
            Selection = () => CellRange.Single(new CellAddress(0, 0)),
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
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=\"house\"");
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
        var status = Fake<PulseOffice.UI.Controls.Services.IStatusBarService>();
        var commands = new LedgerSpellingCommands(context, Fake<ISheetSpelling>(),
            Fake<PulseOffice.Text.Spelling.ISpellDictionaryProvider>(), Fake<PulseOffice.Text.Spelling.IUserDictionary>(),
            Fake<PulseOffice.Text.Spelling.IThesaurusProvider>(), Fake<PulseOffice.Text.Spelling.IHyphenationProvider>(),
            Fake<PulseOffice.UI.Controls.Services.IDialogService>(), status,
            texts, Fake<ISheetFormattingService>(), Fake<ISheetEditGuard>(), Fake<PulseOffice.UI.Controls.Spelling.ISpellingPrompt>(),
            Fake<PulseOffice.UI.Controls.Spelling.IThesaurusPrompt>(), Fake<PulseOffice.UI.Controls.Spelling.ISpellingLanguagePicker>(),
            Fake<PulseOffice.UI.Controls.Spelling.IDictionaryNotice>());

        await commands.GetCommands().Single(c => c.Id == "ledger.spelling.thesaurus").Execute(null!, CancellationToken.None);

        var log = ((Inert)(object)status).Log;
        lock (log)
            Assert.Contains(log, call => call.Method == "Report" && Equals(call.Arguments[0], texts["thesaurus.noWord"]));
    }

    [Fact]
    public void LaModificaEsternaRicaricaOgniSalvataggioNonSoloIlPrimo()
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
        var raster = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "img", MediaType = "image/png", Data = new byte[] { 1 }, PixelWidth = 1, PixelHeight = 1 };
        var placed = new PlacedObject { Id = "img", Content = raster, Anchor = new CellAddress(0, 0), Size = new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(100), new PulseOffice.Foundation.Units.Twip(100)) };
        workbook.Sheets[0].Objects.Add(placed);
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = Engines.Create(workbook),
            Selection = () => CellRange.Single(new CellAddress(0, 0)),
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
            ReadSelectedObject = () => placed,
            ReadSelectedObjects = () => [placed],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
        var commands = new LedgerWebCommands(context, Fake<PulseOffice.UI.Controls.Objects.IRasterImageOperations>(),
            Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        commands.Reload(workbook, "img", raster with { Data = new byte[] { 2 }, PixelWidth = 2 });
        commands.Reload(workbook, "img", raster with { Data = new byte[] { 3 }, PixelWidth = 3 });

        var current = (PulseOffice.Drawing.Assets.RasterImageObject)workbook.Sheets[0].Objects.Single().Content;
        Assert.Equal([3], current.Data.ToArray());
        Assert.Equal(3, current.PixelWidth);
        history.Undo();
        Assert.Equal([2], ((PulseOffice.Drawing.Assets.RasterImageObject)workbook.Sheets[0].Objects.Single().Content).Data.ToArray());
    }

    private sealed class CountingStructure(Engine.Operations.ISheetStructure inner) : Engine.Operations.ISheetStructure
    {
        public int Deletions { get; private set; }

        public void InsertRows(Engine.Recalculation.CalculationEngine engine, int sheetIndex, int index, int count) => inner.InsertRows(engine, sheetIndex, index, count);

        public void DeleteRows(Engine.Recalculation.CalculationEngine engine, int sheetIndex, int index, int count)
        {
            Deletions++;
            inner.DeleteRows(engine, sheetIndex, index, count);
        }

        public void InsertColumns(Engine.Recalculation.CalculationEngine engine, int sheetIndex, int index, int count) => inner.InsertColumns(engine, sheetIndex, index, count);

        public void DeleteColumns(Engine.Recalculation.CalculationEngine engine, int sheetIndex, int index, int count) => inner.DeleteColumns(engine, sheetIndex, index, count);

        public void ShiftBand(Engine.Recalculation.CalculationEngine engine, int sheetIndex, Engine.Operations.StructureChange change) => inner.ShiftBand(engine, sheetIndex, change);

        public bool SplitsBlocks(Workbook workbook, int sheetIndex, Engine.Operations.StructureChange change) => inner.SplitsBlocks(workbook, sheetIndex, change);

        public IReadOnlyList<CellRange> Blocks(Workbook workbook, int sheetIndex) => inner.Blocks(workbook, sheetIndex);

        public IReadOnlyList<CellRange> RigidBlocks(Workbook workbook, int sheetIndex) => inner.RigidBlocks(workbook, sheetIndex);
    }

    [Fact]
    public async Task RimuoviDuplicatiSpostaLeCelleDellAreaSenzaCancellareRigheIntere()
    {
        var workbook = Workbook.CreateDefault();
        var engine = Engines.Create(workbook);
        var history = new UndoStack();
        string[] values = ["a", "a", "a", "a", "b", "b", "c"];
        for (var row = 0; row < values.Length; row++)
            engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), PulseOffice.Sheets.Values.CellValue.Text(values[row]));
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = engine,
            Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(values.Length - 1, 0)),
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
        var structure = new CountingStructure(new Engine.Operations.SheetStructure());
        var snapshots = new Engine.Operations.SheetSnapshots();
        var commands = new LedgerDataCommands(context, Fake<Engine.Operations.ISheetOutline>(), new Engine.Operations.DuplicateRemover(new PulseOffice.Sheets.Addressing.SelectionWorkArea()),
            new Engine.Operations.SheetCellShift(structure), Fake<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            new Engine.Operations.WorkbookCheckpoints(snapshots),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));

        await commands.GetCommands().Single(c => c.Id == "ledger.data.removeDuplicates").Execute(null!, CancellationToken.None);

        Assert.Equal(["a", "b", "c"], Enumerable.Range(0, 3).Select(row => workbook.Sheets[0].GetValue(new CellAddress(row, 0)).AsText()));
        Assert.True(workbook.Sheets[0].GetValue(new CellAddress(3, 0)).IsEmpty);
        Assert.Equal(0, structure.Deletions);
        history.Undo();
        Assert.Equal(values, Enumerable.Range(0, values.Length).Select(row => workbook.Sheets[0].GetValue(new CellAddress(row, 0)).AsText()));
    }
}
