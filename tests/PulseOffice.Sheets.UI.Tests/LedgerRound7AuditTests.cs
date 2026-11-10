using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerRound7AuditTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static CellAddress A(string text) => CellAddress.TryParseA1(text, out var address) ? address : throw new ArgumentException(text);

    private static Engine.Operations.IWorkbookCheckpoints Checkpoints() =>
        new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots());

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

    private sealed class RecordingWrites : ISheetValueWrites
    {
        public int Calls { get; private set; }

        public void Write(IActiveSheetContext context, Engine.Recalculation.CalculationEngine engine,
            IReadOnlyList<(CellAddress Address, CellValue Value)> values, string description) => Calls++;

        public void Write(IActiveSheetContext context, Engine.Recalculation.CalculationEngine engine,
            IReadOnlyList<(CellAddress Address, CellValue Value)> values, IReadOnlyList<(CellAddress Address, string Formula)> formulas,
            string description) => Calls++;
    }

    private sealed class FixedSplit(IReadOnlyDictionary<CellAddress, CellValue> values) : Engine.Operations.ITextToColumns
    {
        public Engine.Operations.TextToColumnsPlan Plan(Worksheet sheet, Engine.Operations.TextToColumnsRequest request) => new(values, 2);

        public IReadOnlyList<string> Split(string text, Engine.Operations.TextToColumnsRequest request) => [];
    }

    private sealed class FixedLink(PulseOffice.UI.Controls.Links.LinkChoice choice) : PulseOffice.UI.Controls.Links.IHyperlinkPrompt
    {
        public Task<PulseOffice.UI.Controls.Links.LinkChoice?> AskAsync(PulseOffice.UI.Controls.Links.LinkRequest request, CancellationToken ct = default) =>
            Task.FromResult<PulseOffice.UI.Controls.Links.LinkChoice?>(choice with { Text = request.Text ?? string.Empty });
    }

    private sealed class Scene
    {
        public Workbook Workbook { get; } = Workbook.CreateDefault();
        public UndoStack History { get; } = new();
        public ActiveSheetContext Context { get; } = new();
        public Engine.Recalculation.CalculationEngine Engine { get; }
        public CellRange Selection { get; set; } = CellRange.Single(CellAddress.First);

        public Scene()
        {
            Engine = Engines.Create(Workbook);
            Context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = 0,
                History = History,
                Engine = Engine,
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
                ReadSelectedObject = () => null,
                ReadSelectedObjects = () => [],
                WriteSelectedObjects = _ => { },
                SelectSheet = _ => { },
                WriteSelectedObject = _ => { },
            });
        }
    }

    private static Task Run(IEnumerable<CommandDefinition> commands, string id) =>
        commands.Single(command => command.Id.Value == id).Execute(null!, CancellationToken.None);

    [Fact]
    public async Task InserireCelleCheSpingonoDatiOltreLaFineDelFoglioVieneRifiutato()
    {
        var scene = new Scene();
        var sheet = scene.Workbook.Sheets[0];
        var last = new CellAddress(CellAddress.MaxRows - 1, 0);
        sheet.SetValue(last, CellValue.Text("last"));
        sheet.SetValue(new CellAddress(CellAddress.MaxRows - 2, 0), CellValue.Text("before last"));
        var status = new RecordingStatus();
        var commands = new LedgerCellShiftCommands(scene.Context, new Engine.Operations.SheetCellShift(new Engine.Operations.SheetStructure()),
            Checkpoints(), new AnsweringDialogs(new Dictionary<string, object> { ["shift"] = 1 }), status, Texts,
            Fake<PulseOffice.UI.Icons.IIconCatalog>());

        await Run(commands.GetCommands(), "ledger.cells.insert");

        Assert.Equal("last", sheet[last].Value.AsText());
        Assert.Equal("before last", sheet[new CellAddress(CellAddress.MaxRows - 2, 0)].Value.AsText());
        Assert.Equal([Texts["status.cells.wouldSpill"]], status.Messages);
        Assert.False(scene.History.CanUndo);
    }

    [Fact]
    public async Task RimuovereIDuplicatiNonCancellaLeColonneFuoriDallaSelezione()
    {
        var scene = new Scene();
        var sheet = scene.Workbook.Sheets[0];
        string[] values = ["a", "a", "b"];
        for (var row = 0; row < values.Length; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(values[row]));
            sheet.SetValue(new CellAddress(row, 2), CellValue.Text("keep" + row));
        }

        scene.Selection = new CellRange(A("A1"), A("A3"));
        var commands = new LedgerDataCommands(scene.Context, Fake<Engine.Operations.ISheetOutline>(), new Engine.Operations.DuplicateRemover(new PulseOffice.Sheets.Addressing.SelectionWorkArea()),
            new Engine.Operations.SheetCellShift(new Engine.Operations.SheetStructure()), new RecordingStatus(), Checkpoints(), Texts);

        await Run(commands.GetCommands(), "ledger.data.removeDuplicates");

        Assert.Equal(["a", "b"], new[] { sheet[A("A1")].Value.AsText(), sheet[A("A2")].Value.AsText() });
        Assert.Equal(["keep0", "keep1", "keep2"], new[] { sheet[A("C1")].Value.AsText(), sheet[A("C2")].Value.AsText(), sheet[A("C3")].Value.AsText() });
    }

    [Fact]
    public void IlRifiutoDellOrdinamentoDiceCheSiTrattaDellOrdinamento()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Protection = new SheetProtection { IsEnabled = true };
        var status = new RecordingStatus();

        Assert.True(new SheetSortGate(new SheetEditGuard(), status, Texts, new Engine.Operations.SheetSorter()).Refuses(workbook, sheet, new CellRange(A("A1"), A("A3"))));
        Assert.Equal([Texts["status.protection.sort"]], status.Messages);
    }

    [Fact]
    public async Task TestoInColonneNonScriveNelleColonneBloccateADestra()
    {
        var scene = new Scene();
        var sheet = scene.Workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Text("x;y"));
        sheet.SetStyle(A("A1"), scene.Workbook.Styles.Derive(sheet[A("A1")].StyleId, style => style with { Locked = false }));
        sheet.SetValue(A("B1"), CellValue.Text("guarded"));
        sheet.Protection = new SheetProtection { IsEnabled = true };
        var status = new RecordingStatus();
        var split = new FixedSplit(new Dictionary<CellAddress, CellValue> { [A("A1")] = CellValue.Text("x"), [A("B1")] = CellValue.Text("y") });
        var choice = new Dialogs.TextToColumnsChoice(false, true, false, false, string.Empty, false, Dialogs.TextQualifier.Double, false);
        var commands = new LedgerToolCommands(scene.Context, new Engine.Operations.SheetSnapshots(),
            Fake<Engine.Operations.ISheetSubtotals>(), split, new AnsweringDialogs(new Dictionary<string, object> { ["options"] = choice }), status,
            new SheetEditGuard(), Fake<ISheetSortGate>(), Fake<Dialogs.IValidationRules>(), new Engine.Operations.ValidationPlacement(), Fake<Dialogs.ISheetReferenceSources>(),
            Fake<Dialogs.ISheetReferenceReader>(), Texts, null!, null!, Fake<DataDialogs.ILedgerDialogProbes>(), Fake<ISheetRangeFilters>());

        await Run(commands.GetCommands(), "ledger.data.textToColumns");

        Assert.Equal("guarded", sheet[A("B1")].Value.AsText());
        Assert.Equal([Texts["status.protection.locked"]], status.Messages);
    }

    [Fact]
    public void INomiDiFoglioGeneratiRestanoEntroTrentunoCaratteri()
    {
        var workbook = Workbook.CreateDefault();
        var long31 = new string('x', 31);
        workbook.AddSheet(long31);
        var policy = new WorksheetTabPolicy();

        var copy = policy.UniqueName(workbook, long31, keepWhenFree: false);

        Assert.True(copy.Length <= 31, copy);
        Assert.True(policy.NameAllowed(copy));
        Assert.Null(workbook.FindSheet(copy));
        Assert.False(policy.NameAllowed(new string('y', 32)));
        Assert.Equal("sheet.name.invalid", new DataDialogs.SheetMovePlan(["A"], 0, policy).NameProblem(new string('y', 32), copy: true));
    }

    [Fact]
    public async Task IlModuloDatiNonScriveUnNuovoRecordSopraUnaRigaOccupata()
    {
        var scene = new Scene();
        var sheet = scene.Workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Text("Name"));
        sheet.SetValue(A("B1"), CellValue.Text("City"));
        sheet.SetValue(A("A2"), CellValue.Text("Anna"));
        sheet.SetValue(A("A3"), CellValue.Text("unrelated"));
        scene.Selection = new CellRange(A("A1"), A("B2"));
        var status = new RecordingStatus();
        var writes = new RecordingWrites();
        var answer = new DataDialogs.DataFormChoice(new Dictionary<int, IReadOnlyList<string>> { [1] = ["Bruno", "Roma"] });
        var commands = new LedgerSheetExtraCommands(scene.Context, Fake<ISheetRandomFill>(), Fake<Engine.Operations.ISheetFillAcross>(),
            Checkpoints(), Fake<Dialogs.ISheetReferenceReader>(), Fake<Dialogs.ISheetReferenceSources>(),
            new SheetSelectionSet(), new AnsweringDialogs(answer), Fake<PulseOffice.UI.Controls.Macros.IMacroEventsDialog>(), null!,
            Fake<PulseOffice.Platform.IFileDialogService>(), status, Texts,
            new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
            Fake<DataDialogs.ILedgerDialogProbes>(),
            Fake<Engine.Operations.ISheetRegions>(), Fake<Engine.Operations.ICellEditText>(), Fake<Engine.Operations.ICellInputRecognizer>(),
            writes, Fake<PulseOffice.Formats.ISpreadsheetFormatRegistry>(), new WorksheetTabPolicy());

        await Run(commands.GetCommands(), "ledger.data.form");

        Assert.Equal(0, writes.Calls);
        Assert.Equal([Texts["dataForm.rowOccupied"]], status.Messages);
    }

    [Fact]
    public async Task UnCollegamentoSuUnaFormulaNonNeCancellaLaFormula()
    {
        var scene = new Scene();
        scene.Engine.SetValue(new SheetCell(0, A("B1")), CellValue.Number(41));
        scene.Engine.SetFormula(new SheetCell(0, A("A1")), "B1+1");
        var commands = new LedgerInsertCommands(scene.Context,
            new FixedLink(new PulseOffice.UI.Controls.Links.LinkChoice(PulseOffice.UI.Controls.Links.LinkKind.Web, "https://example.org", string.Empty)), Texts);

        await Run(commands.GetDialogCommands(), "ledger.insert.hyperlink");

        var cell = scene.Workbook.Sheets[0][A("A1")];
        Assert.Equal("B1+1", cell.Formula);
        Assert.Equal("https://example.org", cell.Hyperlink);
        Assert.Equal(ValueKind.Number, cell.Value.Kind);
    }

    [Fact]
    public async Task ILettoreDiFileRifiutaUnFileOltreIlTetto()
    {
        var reader = new PulseOffice.UI.Controls.Objects.ChosenFileReader();
        var file = new PulseOffice.Platform.ChosenFile("big.svg", "big.svg")
        {
            OpenRead = _ => Task.FromResult<Stream>(new MemoryStream(new byte[2048])),
            OpenWrite = _ => throw new NotSupportedException(),
        };

        var content = await reader.ReadAsync(file, 1024, CancellationToken.None);

        Assert.Equal(PulseOffice.UI.Controls.Objects.ChosenFileReadStatus.TooLarge, content.Status);
    }
}
