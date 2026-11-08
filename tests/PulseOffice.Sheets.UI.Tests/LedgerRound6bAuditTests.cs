using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Platform;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerRound6bAuditTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private sealed class RecordingStatus : IStatusBarService
    {
        public List<string> Messages { get; } = [];
        public IReadOnlyList<StatusSegment> Segments => [];
        public IReadOnlyList<BackgroundActivity> Activities => [];
        public event EventHandler? Changed { add { } remove { } }
        public StatusSegment Register(string id, StatusSegmentKind kind) => new(id, kind);
        public void Remove(string id) { }
        public void Report(string message, TimeSpan? duration = null) => Messages.Add(message);
        public void DismissTransient() { }
        public void Report(Notification notification) => Messages.Add(notification.Message);
        public void Report(string message, NotificationSeverity severity, NotificationAction? action = null) => Messages.Add(message);
        public BackgroundActivity BeginActivity(string description, CancellationTokenSource? cancellation = null) => new("a", description, cancellation);
        public void EndActivity(BackgroundActivity activity) { }
    }

    private sealed class AnsweringDialogs(object? answer) : IDialogService
    {
        public bool IsDialogOpen => false;
        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) => Task.FromResult((TResult?)answer);
        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;
        public void Close(object? result) { }
    }

    private sealed class TokenFiles : IFileDialogService
    {
        public CancellationToken Seen { get; private set; }
        public IReadOnlyList<FileFilter> Filters { get; private set; } = [];

        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default)
        {
            Seen = ct;
            Filters = filters;
            return Task.FromResult<ChosenFile?>(null);
        }

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<ChosenFile?>(null);
    }

    private sealed class Scene
    {
        public Workbook Workbook { get; } = Workbook.CreateDefault();
        public UndoStack History { get; } = new();
        public ActiveSheetContext Context { get; } = new();
        public CellRange Selection { get; set; } = CellRange.Single(CellAddress.First);
        public PlacedObject? Selected { get; private set; }

        public Scene()
        {
            Context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = 0,
                History = History,
                Engine = Engines.Create(Workbook),
                Selection = () => Selection,
                WriteSelection = range => Selection = range,
                ReadViewOption = _ => false,
                WriteViewOption = (_, _) => { },
                Refresh = () => { },
                FreezePanes = (_, _) => { },
                SplitPanes = (_, _) => { },
                ReadPanesSplit = () => false,
                ApplyRowFilter = (_, _) => { },
                ReadZoom = () => 1,
                WriteZoom = _ => { },
                ReadSelectedObject = () => Selected,
                ReadSelectedObjects = () => Selected is null ? [] : [Selected],
                WriteSelectedObjects = objects => Selected = objects.FirstOrDefault(),
                SelectSheet = _ => { },
                WriteSelectedObject = placed => Selected = placed,
            });
        }
    }

    private static Task Run(IEnumerable<CommandDefinition> commands, string id, CancellationToken ct = default) =>
        commands.Single(command => command.Id.Value == id).Execute(null!, ct);

    [Fact]
    public async Task GestireLeRegoleSuUnFoglioProtettoUsaLaStessaPoliticaDelFormato()
    {
        var scene = new Scene();
        scene.Workbook.Sheets[0].Protection = new SheetProtection { IsEnabled = true };
        var status = new RecordingStatus();

        var commands = new LedgerConditionCommands(scene.Context, Fake<ISheetFormattingService>(), new AnsweringDialogs(null), status,
            null!, Fake<Engine.Localization.IFormulaLocalization>(), Fake<Conditional.IConditionalRuleCompletion>(),
            Fake<Dialogs.ISheetReferenceSources>(), Fake<Dialogs.ISheetReferenceReader>(), Texts,
            new SheetFormatGate(new SheetEditGuard(), status, Texts));

        await Run(commands.GetCommands(), "ledger.conditional.manage", TestContext.Current.CancellationToken);

        Assert.Equal([Texts["status.protection.cellFormat"]], status.Messages);
    }

    [Fact]
    public void IlCancelloDelFormatoLasciaPassareIFogliCheConsentonoIlFormato()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        sheet.Protection = new SheetProtection { IsEnabled = true, AllowFormatCells = true };

        Assert.False(new SheetFormatGate(new SheetEditGuard(), new RecordingStatus(), Texts).Refuses(sheet));
    }

    [Fact]
    public async Task IlGraficoInseritoSulleUltimeColonneRestaDentroIlFoglio()
    {
        var scene = new Scene
        {
            Selection = new CellRange(new CellAddress(0, CellAddress.MaxColumns - 2), new CellAddress(3, CellAddress.MaxColumns - 1)),
        };

        var commands = new LedgerChartCommands(Fake<ISheetFormattingService>(), scene.Context, new RecordingStatus(), Insertion(scene),
            Fake<PulseOffice.UI.Controls.Charts.IChartPicker>(), Fake<PulseOffice.UI.Controls.Charts.IChartEditor>(),
            Fake<Charts.IChartSettingsMapper>(), Fake<Engine.Operations.IChartDataExtractor>(), Fake<Dialogs.ISheetReferenceSources>(), Texts);

        await Run(commands.GetCommands(), "ledger.chart.column", TestContext.Current.CancellationToken);

        var placed = Assert.Single(scene.Workbook.Sheets[0].Objects);
        Assert.True(placed.Anchor.IsValid);
        Assert.True(placed.Anchor.Column < CellAddress.MaxColumns);
    }

    [Fact]
    public async Task UnNomeDiIntervalloDiDatabaseNonValidoVieneRifiutato()
    {
        var scene = new Scene { Selection = new CellRange(CellAddress.First, new CellAddress(2, 1)) };
        var status = new RecordingStatus();
        var answer = new Dictionary<string, object> { ["name"] = "B2", ["area"] = "A1:B3", ["header"] = true };
        var reader = new Dialogs.SheetReferenceReader(new Engine.Operations.SheetRangeReferences());

        var commands = new LedgerDatabaseRangeCommands(scene.Context, Fake<Engine.Operations.ISheetRegions>(), Fake<IQueryRefreshRunner>(),
            new AnsweringDialogs(answer), status,
            new Dialogs.SheetReferenceSources(scene.Context, reader, new Engine.Operations.SheetRangeReferences(), Fake<PulseOffice.UI.Controls.References.IReferencePickers>()),
            reader, Texts,
            new Engine.Operations.DefinedNameRules());

        await Run(commands.GetCommands(), "ledger.database.define", TestContext.Current.CancellationToken);

        Assert.Equal(0, scene.Workbook.DatabaseRanges.Count);
        Assert.Equal([Texts.Format("names.invalid", "B2")], status.Messages);
    }

    [Fact]
    public async Task Redefining_a_database_range_keeps_its_filters_and_binding()
    {
        var scene = new Scene { Selection = new CellRange(CellAddress.First, new CellAddress(2, 1)) };
        var filter = new ColumnFilter(0) { Criteria = ">1" };
        scene.Workbook.DatabaseRanges.Define(new DatabaseRange
        {
            Name = "Data", SheetIndex = 0, Range = new CellRange(CellAddress.First, new CellAddress(2, 1)),
            ShowsFilterButtons = true, QueryBindingId = "q1", Filters = [filter],
        });
        var answer = new Dictionary<string, object> { ["name"] = "Data", ["area"] = "A1:B6", ["header"] = true };
        var reader = new Dialogs.SheetReferenceReader(new Engine.Operations.SheetRangeReferences());
        var commands = new LedgerDatabaseRangeCommands(scene.Context, Fake<Engine.Operations.ISheetRegions>(), Fake<IQueryRefreshRunner>(),
            new AnsweringDialogs(answer), new RecordingStatus(),
            new Dialogs.SheetReferenceSources(scene.Context, reader, new Engine.Operations.SheetRangeReferences(), Fake<PulseOffice.UI.Controls.References.IReferencePickers>()),
            reader, Texts, new Engine.Operations.DefinedNameRules());

        await Run(commands.GetCommands(), "ledger.database.define", TestContext.Current.CancellationToken);

        var range = scene.Workbook.DatabaseRanges.Find("Data")!;
        Assert.Equal(new CellRange(CellAddress.First, new CellAddress(5, 1)), range.Range);
        Assert.True(range.ShowsFilterButtons);
        Assert.Equal("q1", range.QueryBindingId);
        Assert.Equal([filter], range.Filters);
    }

    [Fact]
    public async Task InserireUnFoglioDaFileRispettaIlTokenEOffreTuttiIFormatiLeggibili()
    {
        var scene = new Scene();
        var files = new TokenFiles();
        using var cancellation = new CancellationTokenSource();
        var formats = new PulseOffice.Formats.SpreadsheetFormatRegistry(
            [new PulseOffice.Formats.Odf.OdsSpreadsheetReader()], [new PulseOffice.Formats.Odf.OdsSpreadsheetWriter()], new PulseOffice.Formats.Intake.DocumentIntake());

        var commands = new LedgerSheetExtraCommands(scene.Context, Fake<ISheetRandomFill>(), Fake<Engine.Operations.ISheetFillAcross>(),
            Fake<Engine.Operations.IWorkbookCheckpoints>(), Fake<Dialogs.ISheetReferenceReader>(), Fake<Dialogs.ISheetReferenceSources>(),
            new SheetSelectionSet(), new AnsweringDialogs(null), Fake<PulseOffice.UI.Controls.Macros.IMacroEventsDialog>(), null!,
            files, new RecordingStatus(), Texts, null!, Fake<DataDialogs.ILedgerDialogProbes>(), Fake<Engine.Operations.ISheetRegions>(),
            Fake<Engine.Operations.ICellEditText>(), Fake<Engine.Operations.ICellInputRecognizer>(), Fake<ISheetValueWrites>(), formats, new WorksheetTabPolicy());

        await Run(commands.GetCommands(), "ledger.sheet.fromFile", cancellation.Token);

        Assert.Equal(cancellation.Token, files.Seen);
        Assert.Contains(files.Filters, filter => filter.Extensions.Contains("*.ods"));
    }

    private sealed class UnreadableSource : PulseOffice.Formats.Interchange.ITabularSource
    {
        public ExternalDataFormat Format => ExternalDataFormat.Csv;
        public IReadOnlyList<string> Tables(string text) => [string.Empty];
        public PulseOffice.Formats.Interchange.TabularData Read(string text, string table) => throw new InvalidDataException("broken");
    }

    private sealed class UnreadableExternal : ExternalData.IExternalDataService
    {
        public Task<ExternalData.ExternalDocument> OpenAsync(string location, ExternalDataFormat format, string? baseDirectory, CancellationToken ct) =>
            Task.FromResult(new ExternalData.ExternalDocument("x", new UnreadableSource()));
        public bool IsFailure(Exception exception) => exception is InvalidDataException;
    }

    private sealed class CountingWriter : Engine.Operations.IExternalDataWriter
    {
        public int Writes { get; private set; }
        public CellRange Write(Worksheet sheet, CellAddress anchor, IReadOnlyList<IReadOnlyList<string>> rows, CellRange? previous)
        {
            Writes++;
            return CellRange.Single(anchor);
        }
        public void WriteStream(Worksheet sheet, CellRange range, IReadOnlyList<IReadOnlyList<string>> lines, StreamInsertion insertion) => Writes++;
    }

    [Fact]
    public async Task AggiornareUnaFonteIlleggibileNonCancellaIDatiNeAggiungeUnPassoVuoto()
    {
        var scene = new Scene();
        scene.Workbook.ExternalData.Replace(new AreaLink
        {
            SheetIndex = 0,
            Anchor = CellAddress.First,
            Url = "data.csv",
            Format = ExternalDataFormat.Csv,
        });
        var writer = new CountingWriter();

        var commands = new LedgerTailCommands(scene.Context, new AnsweringDialogs(null), new RecordingStatus(), Texts,
            new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()), new UnreadableExternal(), writer,
            Fake<PulseOffice.Shell.IDocumentWorkspace>(), Fake<IFileDialogService>(), Fake<ExternalData.IExternalLinkSchedule>(),
            Fake<PulseOffice.Data.UI.ITabularSnapshots>(), Fake<PulseOffice.Data.UI.IDataTableRenderer>(), Fake<Dialogs.ISheetReferenceSources>(),
            Fake<Dialogs.ISheetReferenceReader>(), null!, new Engine.Operations.DefinedNameRules());

        await Run(commands.GetCommands(), "ledger.data.providerRefresh", TestContext.Current.CancellationToken);

        Assert.Equal(0, writer.Writes);
        Assert.False(scene.History.CanUndo);
    }

    private sealed class FixedGoalSeek : Engine.Operations.IGoalSeek
    {
        public Engine.Operations.GoalSeekOutcome Solve(Engine.Recalculation.CalculationEngine engine, Engine.Operations.GoalSeekRequest request) => new(true, 5, 10, 1);
    }

    private sealed class SilentGoalSeekProbe : DataDialogs.IGoalSeekProbe
    {
        public DataDialogs.GoalSeekProbe Probe(DataDialogs.GoalSeekChoice choice, bool solve) => new(null);
    }

    public class Probes : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) => new SilentGoalSeekProbe();
    }

    [Fact]
    public async Task LaRicercaObiettivoNonScriveUnaCellaBloccataDiUnAltroFoglioProtetto()
    {
        var scene = new Scene();
        var other = scene.Workbook.AddSheet("Other");
        other.SetValue(CellAddress.First, Values.CellValue.Number(1));
        other.Protection = new SheetProtection { IsEnabled = true };
        var status = new RecordingStatus();
        var reader = new Dialogs.SheetReferenceReader(new Engine.Operations.SheetRangeReferences());
        var sources = new Dialogs.SheetReferenceSources(scene.Context, reader, new Engine.Operations.SheetRangeReferences(), Fake<PulseOffice.UI.Controls.References.IReferencePickers>());
        var kit = new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter());

        var commands = new LedgerAnalysisCommands(scene.Context, new FixedGoalSeek(), Fake<Engine.Operations.ISolver>(), Fake<Engine.Operations.IScenarioManager>(),
            Fake<Engine.Operations.IDependencyTracer>(), Fake<Analysis.ISolverConstraintRows>(), Fake<Dialogs.IScenarioPlanner>(),
            new AnsweringDialogs(new DataDialogs.GoalSeekChoice("A1", 10, "Other!A1")), status, sources, reader, Texts, kit,
            System.Reflection.DispatchProxy.Create<DataDialogs.ILedgerDialogProbes, Probes>(), new SheetEditGuard(), new PulseOffice.UI.Controls.Preview.DispatcherDebounceTimerFactory());

        await Run(commands.GetCommands(), "ledger.analysis.goalSeek", TestContext.Current.CancellationToken);

        Assert.Equal(1, other[CellAddress.First].Value.AsNumber());
        Assert.False(scene.History.CanUndo);
        Assert.Equal([Texts["status.protection.locked"]], status.Messages);
    }

    private static SheetImageInsertion Insertion(Scene scene) =>
        new(scene.Context, null!, Texts, new PulseOffice.Shell.DocumentWorkspace(), new RecordingStatus(), new ObjectOrdering(), new PulseOffice.Drawing.Assets.ImageExtents());

    [Fact]
    public void UnOggettoInseritoVaSopraQuelliGiaRiordinati()
    {
        var scene = new Scene();
        var sheet = scene.Workbook.Sheets[0];
        PlacedObject Existing(string id, int order) => new()
        {
            Id = id,
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle, Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default },
            Anchor = CellAddress.First,
            ZOrder = order,
        };
        sheet.Objects.AddRange([Existing("a", 0), Existing("b", 1)]);

        var placed = Insertion(scene).Insert(Existing("c", 0), "insert");

        Assert.Same(placed, new ObjectOrdering().Drawn(sheet)[^1]);
    }

    private sealed class RecordingFormatting : ISheetFormattingService
    {
        public List<Styling.CellStyle> Applied { get; } = [];
        public bool CanFormat => true;
        public Styling.CellStyle CurrentStyle => Styling.CellStyle.Default;
        public bool Accepts(string commandId) => true;
        public void ApplyToSelection(Func<Styling.CellStyle, Styling.CellStyle> change) => Applied.Add(change(Styling.CellStyle.Default));
        public void ApplyToSelection(string description, Func<Styling.CellStyle, Styling.CellStyle> change) => Applied.Add(change(Styling.CellStyle.Default));
        public void ApplyToSelection(string description, Func<CellRange, CellAddress, Styling.CellStyle, Styling.CellStyle> change) { }
        public void ToggleOnSelection(Func<Styling.CellStyle, bool> read, Func<Styling.CellStyle, bool, Styling.CellStyle> write) { }
    }

    private static LedgerRibbonCommands Ribbon(Scene scene, ISheetFormattingService formatting) =>
        new(scene.Context, formatting, Fake<ICellFormatDialog>(), Fake<PulseOffice.UI.Controls.IColorDialog>(), new Styling.BorderPresets(),
            Fake<PulseOffice.UI.Commands.IRibbonPaletteBuilder>(), Fake<Formatting.INumberFormatParser>(), Fake<Formatting.INumberFormatter>(), Texts,
            Fake<PulseOffice.UI.Controls.Services.IRibbonMemory>(), Fake<PulseOffice.UI.Controls.Prompts.IZoomPrompt>(), new Formatting.SystemDateCode());

    [Theory]
    [InlineData(1e9, 409)]
    [InlineData(-3, 1)]
    public async Task LaDimensioneDelCarattereDaArgomentoHaUnTetto(double asked, double expected)
    {
        var scene = new Scene();
        var formatting = new RecordingFormatting();

        await Ribbon(scene, formatting).GetCommands().Single(command => command.Id.Value == "ledger.font.size")
            .Execute(new CommandContext(PulseOffice.Foundation.Modules.ModuleKind.Sheets, SelectionKind.Cell, DocumentState.DocumentOpen) { Argument = asked }, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.Single(formatting.Applied).Font.Size);
    }
}
