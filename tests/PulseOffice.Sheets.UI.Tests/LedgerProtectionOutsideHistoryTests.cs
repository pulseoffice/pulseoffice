using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerProtectionOutsideHistoryTests
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
        public int Edits { get; private set; }
        public int OutsideChanges { get; private set; }

        public Scene()
        {
            History.ChangedOutsideHistory += (_, _) => OutsideChanges++;
            Context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = 0,
                History = History,
                Engine = Engines.Create(Workbook),
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
        }

        public void Edit()
        {
            Edits++;
            History.Push(new DelegateAction("edit", () => Edits++, () => Edits--));
        }
    }

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static DataDialogs.LedgerDialogKit Kit() =>
        new(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter());

    private static CommandDefinition SheetProtect(Scene scene, object answer) =>
        new LedgerSheetCommands(scene.Context, new AnsweringDialogs(answer), Fake<IStatusBarService>(), Texts,
                Fake<Engine.Operations.ISheetDuplicator>(), Fake<Engine.Operations.ISheetRenaming>(), Fake<Engine.Operations.IChartSheetReferences>(), Fake<IWorksheetTabPolicy>(),
                Kit(), Fake<IRibbonPaletteBuilder>())
            .GetCommands().Single(command => command.Id.Value == "ledger.worksheet.protect");

    private static CommandDefinition StructureProtect(Scene scene, object answer) =>
        new LedgerPartialCommands(scene.Context, Fake<ISeriesFill>(), Fake<Engine.Operations.IStandardFilter>(), Fake<ISheetRangeFilters>(),
                Fake<ILedgerDocumentFactory>(), Fake<PulseOffice.Shell.IDocumentWorkspace>(), new AnsweringDialogs(answer),
                Fake<IStatusBarService>(), Texts, Fake<Engine.Operations.ISheetDuplicator>(), Kit(),
                Fake<DataDialogs.ISeriesPreviewers>(), Fake<ISheetValueWrites>(), new WorksheetTabPolicy())
            .GetCommands().Single(command => command.Id.Value == "ledger.file.protectStructure");

    private static CommandDefinition ChangesProtect(Scene scene, object answer) =>
        new LedgerTailCommands(scene.Context, new AnsweringDialogs(answer), Fake<IStatusBarService>(), Texts,
                new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()),
                Fake<ExternalData.IExternalDataService>(), Fake<Engine.Operations.IExternalDataWriter>(),
                Fake<PulseOffice.Shell.IDocumentWorkspace>(), Fake<PulseOffice.Platform.IFileDialogService>(),
                Fake<ExternalData.IExternalLinkSchedule>(), Fake<PulseOffice.Data.UI.ITabularSnapshots>(),
                Fake<PulseOffice.Data.UI.IDataTableRenderer>(), Fake<Dialogs.ISheetReferenceSources>(),
                Fake<Dialogs.ISheetReferenceReader>(), Kit(), new Engine.Operations.DefinedNameRules())
            .GetCommands().Single(command => command.Id.Value == "ledger.review.protectChanges");

    private static CommandDefinition Record(Scene scene) =>
        new LedgerChangeCommands(scene.Context, Fake<PulseOffice.UI.Controls.ISidePanelHost>(), Fake<IStatusBarService>(), Texts)
            .GetCommands().Single(command => command.Id.Value == "ledger.changes.record");

    private static async Task Run(CommandDefinition command) => await command.Execute(null!, CancellationToken.None);

    public static TheoryData<string> Protections => ["sheet", "structure", "changes"];

    private static (CommandDefinition Command, Func<bool> Protected) Protection(Scene scene, string kind) => kind switch
    {
        "sheet" => (SheetProtect(scene, new DataDialogs.SheetProtectionChoice(string.Empty, new SheetProtection())),
            () => scene.Workbook.Sheets[0].Protection.IsEnabled),
        "structure" => (StructureProtect(scene, new Dictionary<string, object> { ["structure"] = true, ["windows"] = false, ["password"] = string.Empty }),
            () => scene.Workbook.Protection.Structure),
        _ => (ChangesProtect(scene, new Dictionary<string, object> { ["protected"] = true, ["password"] = string.Empty }),
            () => scene.Workbook.Changes.IsProtected),
    };

    [Theory]
    [MemberData(nameof(Protections))]
    public async Task ProteggereNonEntraNelloStoricoEDaComunqueUnaModifica(string kind)
    {
        var scene = new Scene();
        var (command, isProtected) = Protection(scene, kind);

        await Run(command);
        scene.History.Undo();

        Assert.True(command.OutsideHistory);
        Assert.True(isProtected());
        Assert.False(scene.History.CanUndo);
        Assert.Equal(1, scene.OutsideChanges);
    }

    [Theory]
    [MemberData(nameof(Protections))]
    public async Task AnnullareERipetereUnaModificaPrecedenteNonToccaLaProtezione(string kind)
    {
        var scene = new Scene();
        var (command, isProtected) = Protection(scene, kind);

        scene.Edit();
        await Run(command);
        scene.History.Undo();

        Assert.Equal(0, scene.Edits);
        Assert.True(isProtected());

        scene.History.Redo();

        Assert.Equal(1, scene.Edits);
        Assert.True(isProtected());
    }

    [Fact]
    public async Task RegistrareLeModificheEsceDalloStorico()
    {
        var scene = new Scene();
        var record = Record(scene);

        scene.Edit();
        await Run(record);
        scene.History.Undo();

        Assert.True(record.OutsideHistory);
        Assert.Equal(0, scene.Edits);
        Assert.True(scene.Workbook.Changes.IsRecording);
        Assert.Equal(1, scene.OutsideChanges);

        scene.History.Redo();
        Assert.True(scene.Workbook.Changes.IsRecording);
    }

    [Fact]
    public async Task UnoScattoPrecedenteNonRimetteLaProtezioneDelleModificheNeLaRegistrazione()
    {
        var scene = new Scene();
        var checkpoints = new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots());
        var engine = scene.Context.Engine!;
        var before = checkpoints.Capture(scene.Workbook);

        await Run(Record(scene));
        await Run(ChangesProtect(scene, new Dictionary<string, object> { ["protected"] = true, ["password"] = string.Empty }));
        checkpoints.Restore(engine, scene.Workbook, before);

        Assert.True(scene.Workbook.Changes.IsProtected);
        Assert.True(scene.Workbook.Changes.IsRecording);
    }
}
