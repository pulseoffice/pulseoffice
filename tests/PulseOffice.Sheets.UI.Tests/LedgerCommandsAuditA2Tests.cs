using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Platform;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerCommandsAuditA2Tests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.TextCatalog Texts = new(PulseOffice.Localization.UiLanguage.English);

    private static PulseOffice.UI.Controls.Services.StatusBarService Status() => new(
        TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
        new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));

    private static CommandContext Run => new(PulseOffice.Foundation.Modules.ModuleKind.Sheets, SelectionKind.None, DocumentState.DocumentOpen);

    private sealed class Harness
    {
        public required Workbook Workbook { get; init; }
        public required UndoStack History { get; init; }
        public required ActiveSheetContext Context { get; init; }
        public CellRange Selection { get; set; }
        public List<PlacedObject> SelectedObjects { get; set; } = [];
    }

    private static Harness Build(CellRange selection)
    {
        var workbook = Workbook.CreateDefault();
        var harness = new Harness { Workbook = workbook, History = new UndoStack(), Context = new ActiveSheetContext(), Selection = selection };
        var zoom = 1.0;

        harness.Context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = harness.History,
            Engine = Engines.Create(workbook),
            Selection = () => harness.Selection,
            WriteSelection = range => harness.Selection = range,
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => zoom,
            WriteZoom = value => zoom = value,
            ReadSelectedObject = () => harness.SelectedObjects.FirstOrDefault(),
            ReadSelectedObjects = () => harness.SelectedObjects,
            WriteSelectedObjects = objects => harness.SelectedObjects = [.. objects],
            SelectSheet = _ => { },
            WriteSelectedObject = placed => harness.SelectedObjects = placed is null ? [] : [placed],
        });

        return harness;
    }

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public async Task SelezionareLeRigheVisibiliEscludeLaPrimaRigaNascosta()
    {
        var harness = Build(new CellRange(A("A1"), A("C4")));
        harness.Workbook.Sheets[0].Layout.HideRow(0);

        var commands = new LedgerSelectionExtraCommands(harness.Context, new SheetSelectionSet(), null!, Status(), Texts);
        await commands.GetCommands().Single(command => command.Id.Value == "ledger.select.visibleRows").Execute(Run, CancellationToken.None);

        Assert.Equal("A2:C4", harness.Selection.ToString());
    }

    [Fact]
    public async Task SelezionareLeColonneVisibiliEscludeLUltimaColonnaNascosta()
    {
        var harness = Build(new CellRange(A("A1"), A("C4")));
        harness.Workbook.Sheets[0].Layout.HideColumn(2);

        var commands = new LedgerSelectionExtraCommands(harness.Context, new SheetSelectionSet(), null!, Status(), Texts);
        await commands.GetCommands().Single(command => command.Id.Value == "ledger.select.visibleColumns").Execute(Run, CancellationToken.None);

        Assert.Equal("A1:B4", harness.Selection.ToString());
    }

    [Fact]
    public void AnnullareUnaScritturaDiValoriRipristinaLeFormule()
    {
        var harness = Build(CellRange.Single(A("A1")));
        var engine = harness.Context.Engine!;
        engine.SetValue(new SheetCell(0, A("A1")), CellValue.Number(2));
        engine.SetFormula(new SheetCell(0, A("B1")), "=A1*3");

        new SheetValueWrites(new Engine.Operations.SheetSnapshots())
            .Write(harness.Context, engine, [(A("B1"), CellValue.Number(7)), (A("C1"), CellValue.Text("x"))], "fill");

        var sheet = harness.Workbook.Sheets[0];
        Assert.False(sheet[A("B1")].HasFormula);

        harness.History.Undo();

        Assert.True(sheet[A("B1")].HasFormula);
        Assert.Equal(6, sheet[A("B1")].Value.AsNumber());
        Assert.True(sheet[A("C1")].Value.IsEmpty);

        harness.History.Redo();

        Assert.Equal(7, sheet[A("B1")].Value.AsNumber());
        Assert.Equal("x", sheet[A("C1")].Value.AsText());
    }

    [Fact]
    public async Task AnnullareLaCancellazioneDiUnOggettoNeRipristinaLOrdine()
    {
        var harness = Build(CellRange.Single(A("A1")));
        var sheet = harness.Workbook.Sheets[0];
        PlacedObject Shape(string id) => new()
        {
            Id = id,
            Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle, Style = ShapeStyle.Default },
            Anchor = CellAddress.First,
            Size = new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1)),
        };
        sheet.Objects.AddRange([Shape("a"), Shape("b"), Shape("c")]);
        harness.SelectedObjects = [sheet.Objects[0]];

        var commands = new LedgerShapeCommands(null!, null!, harness.Context, null!, Status(), null!, null!, null!, null!, null!, null!, null!, null!, null!, Texts, new PulseOffice.UI.Controls.Objects.ChosenFileReader(), new PulseOffice.Drawing.Shapes.DimensionLineTemplate());
        await commands.GetCommands().Single(command => command.Id.Value == "ledger.object.delete").Execute(Run, CancellationToken.None);

        Assert.Equal(["b", "c"], sheet.Objects.Select(placed => placed.Id));

        harness.History.Undo();

        Assert.Equal(["a", "b", "c"], sheet.Objects.Select(placed => placed.Id));
    }

    private sealed class OneFileDialog(byte[] content) : IFileDialogService
    {
        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<ChosenFile?>(new ChosenFile("/tmp/fonte.ods", "fonte.ods")
            {
                OpenRead = _ => Task.FromResult<Stream>(new MemoryStream(content)),
                OpenWrite = _ => throw new NotSupportedException(),
            });

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<ChosenFile?>(null);
    }

    [Fact]
    public async Task InserireFogliDaFileSiAnnulla()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(5));
        using var buffer = new MemoryStream();
        new Formats.Odf.OdsWriter().Write(source, buffer);

        var harness = Build(CellRange.Single(A("A1")));
        var before = harness.Workbook.Sheets.Count;
        var commands = new LedgerSheetExtraCommands(
            harness.Context, null!, null!, new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()),
            null!, null!, new SheetSelectionSet(), null!, null!, null!, new OneFileDialog(buffer.ToArray()), Status(), Texts,
            null!, null!, null!, null!, null!, null!,
            new Formats.SpreadsheetFormatRegistry([new Formats.Odf.OdsSpreadsheetReader()], [new Formats.Odf.OdsSpreadsheetWriter()], new Formats.Intake.DocumentIntake()), new WorksheetTabPolicy());

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.sheet.fromFile").Execute(Run, CancellationToken.None);

        Assert.Equal(before + source.Sheets.Count, harness.Workbook.Sheets.Count);
        Assert.Equal(5, harness.Workbook.Sheets[^1][A("A1")].Value.AsNumber());

        harness.History.Undo();

        Assert.Equal(before, harness.Workbook.Sheets.Count);
    }
}
