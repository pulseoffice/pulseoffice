using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class SheetsUiRootAuditTests
{
    [Fact]
    public void La_procedura_guidata_descrive_la_categoria_nella_lingua_dell_interfaccia()
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
        var wizard = new FunctionWizardView(
            provider.GetRequiredService<IFunctionRegistry>(), texts, provider.GetRequiredService<IFormulaLocalization>(),
            new FunctionCategoryLooks(texts), new PulseOffice.UI.Icons.IconCatalog());

        var description = wizard.DescriptionText;

        Assert.StartsWith("Category: ", description);
        Assert.DoesNotContain("Statistica", description);
        Assert.DoesNotContain("Matematica", description);
        Assert.DoesNotContain("Testo", description);
    }

    [Fact]
    public void Un_colore_di_filtro_malformato_letto_dal_file_non_impedisce_di_aprire_il_menu()
    {
        var swatches = new FilterSwatchColours();
        var malformed = new ColorFilterCriterion(ColorFilterTarget.Background, "#ZZZZZZ");

        Assert.Equal(Avalonia.Media.Colors.White, swatches.Fill(malformed));
        Assert.Equal(Avalonia.Media.Colors.Black, swatches.Ink(malformed with { Target = ColorFilterTarget.Text }));
        Assert.Equal(Avalonia.Media.Color.Parse("#FF0000"), swatches.Fill(new ColorFilterCriterion(ColorFilterTarget.Background, "#FF0000")));
    }

    [Fact]
    public void Rifiutare_una_modifica_dal_pannello_non_scrive_le_celle_bloccate_ne_risolve_una_registrazione_protetta()
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var workbook = Workbook.CreateDefault();
        var engine = provider.GetRequiredService<PulseOffice.Sheets.Engine.Recalculation.ICalculationEngineFactory>().Create(workbook);
        var history = new PulseOffice.Document.Commands.UndoStack();
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, engine, history));
        var sheet = workbook.Sheets[0];
        var address = new PulseOffice.Sheets.Addressing.CellAddress(0, 0);
        engine.SetValue(new SheetCell(0, address), PulseOffice.Sheets.Values.CellValue.Number(9));
        workbook.Changes.Import([new TrackedChange
        {
            Id = "c1", SheetIndex = 0, Address = address,
            Before = PulseOffice.Sheets.Values.CellValue.Number(1), After = PulseOffice.Sheets.Values.CellValue.Number(9),
        }]);
        sheet.Protection = new SheetProtection { IsEnabled = true };
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
        var resolution = new LedgerChangeResolution(context,
            new PulseOffice.Sheets.Engine.Operations.WorkbookCheckpoints(new PulseOffice.Sheets.Engine.Operations.SheetSnapshots()),
            new SheetEditGuard(), Status(), texts);

        Assert.Equal(0, resolution.Resolve(["c1"], false, "reject"));
        Assert.Equal(9, sheet[address].Value.AsNumber());
        Assert.Equal(ChangeState.Pending, workbook.Changes.Changes[0].State);

        sheet.Protection = new SheetProtection();
        workbook.Changes.IsProtected = true;
        Assert.Equal(0, resolution.Resolve(["c1"], true, "accept"));
        Assert.Equal(ChangeState.Pending, workbook.Changes.Changes[0].State);
    }

    [Fact]
    public void Il_navigatore_non_riordina_i_fogli_di_una_struttura_protetta()
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Due");
        var engine = provider.GetRequiredService<PulseOffice.Sheets.Engine.Recalculation.ICalculationEngineFactory>().Create(workbook);
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, engine, new PulseOffice.Document.Commands.UndoStack()));
        var navigator = new LedgerNavigatorSource(context, new Dialogs.SheetReferenceReader(new PulseOffice.Sheets.Engine.Operations.SheetRangeReferences()),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));
        var first = workbook.Sheets[0].Name;
        workbook.Protection = new WorkbookProtection { Structure = true };

        Assert.False(navigator.CanMove("sheets", "0", PulseOffice.UI.Controls.Navigation.NavigatorMove.Down));
        Assert.False(navigator.CanDrop("sheets", "0", null));
        navigator.Move("sheets", "0", PulseOffice.UI.Controls.Navigation.NavigatorMove.Down);
        navigator.Drop("sheets", "0", null);

        Assert.Equal(first, workbook.Sheets[0].Name);
    }

    [Fact]
    public void Le_operazioni_sul_documento_usano_il_documento_indicato_e_non_quello_attivo()
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var active = Workbook.CreateDefault();
        var other = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        context.Attach(Target(active, provider.GetRequiredService<PulseOffice.Sheets.Engine.Recalculation.ICalculationEngineFactory>().Create(active),
            new PulseOffice.Document.Commands.UndoStack()));
        var operations = new LedgerDocumentOperations(context, null!, null!, null!, null!, null!, null!, null!,
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English));
        var document = new PulseOffice.Shell.OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Sheets, "Altro",
            new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack()) { Model = other };

        Assert.Same(other.Properties, operations.Properties(document));
        Assert.Same(other.Versions, operations.Versions(document));
    }

    [Fact]
    public void Un_immagine_acquisita_non_entra_in_un_foglio_protetto()
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, provider.GetRequiredService<PulseOffice.Sheets.Engine.Recalculation.ICalculationEngineFactory>().Create(workbook),
            new PulseOffice.Document.Commands.UndoStack()));
        workbook.Sheets[0].Protection = new SheetProtection { IsEnabled = true };
        var insertion = new SheetImageInsertion(context, null!,
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English), new PulseOffice.Shell.DocumentWorkspace(), Status(), new ObjectOrdering(), new PulseOffice.Drawing.Assets.ImageExtents());

        var inserted = insertion.InsertObject(new PulseOffice.Drawing.Assets.RasterImageObject { Id = "scan", Data = new byte[] { 1 }, MediaType = "image/png", PixelWidth = 1, PixelHeight = 1 },
            new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromPoints(10), PulseOffice.Foundation.Units.Twip.FromPoints(10)), "insert");

        Assert.False(inserted);
        Assert.Empty(workbook.Sheets[0].Objects);
    }

    [Fact]
    public void Una_risposta_web_enorme_non_si_legge_per_intero()
    {
        var body = new CountingStream(64L * 1024 * 1024);
        using var web = new WebResourceAccess(new WebAllowed(), new HandlerClients(new StreamHandler(body)));

        var text = web.Fetch("https://example.test/data");

        Assert.NotNull(text);
        Assert.Equal(2 * 1024 * 1024, text!.Length);
        Assert.True(body.Served < 8L * 1024 * 1024, $"served {body.Served} bytes");
    }

    [Fact]
    public void Le_opzioni_di_vista_seguono_il_documento_attivo_e_non_l_ultimo_creato()
    {
        var workspace = new PulseOffice.Shell.DocumentWorkspace();
        var first = new OptionsView();
        var second = new OptionsView();
        var firstDocument = new PulseOffice.Shell.OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Sheets, "Uno", first, new PulseOffice.Document.Commands.UndoStack());
        workspace.Open(firstDocument);
        workspace.Open(new PulseOffice.Shell.OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Sheets, "Due", second, new PulseOffice.Document.Commands.UndoStack()));
        workspace.Activate(firstDocument);
        var options = new ActiveShellViewOptions(workspace);

        options.FormulaBarVisible = false;

        Assert.False(first.FormulaBarVisible);
        Assert.True(second.FormulaBarVisible);
    }

    private sealed class OptionsView : Avalonia.Controls.Control, IShellViewOptions
    {
        public bool FormulaBarVisible { get; set; } = true;

        public bool SheetTabsVisible { get; set; } = true;

        public bool HeadersVisible { get; set; } = true;
    }

    [Fact]
    public void Sostituisci_tutto_nello_storico_ha_il_nome_nella_lingua_dell_interfaccia()
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var workbook = Workbook.CreateDefault();
        var engine = provider.GetRequiredService<PulseOffice.Sheets.Engine.Recalculation.ICalculationEngineFactory>().Create(workbook);
        var history = new PulseOffice.Document.Commands.UndoStack();
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, engine, history));
        workbook.Sheets[0][PulseOffice.Sheets.Addressing.CellAddress.First] = new Cell { Value = PulseOffice.Sheets.Values.CellValue.Text("alfa") };
        workbook.Sheets[0][new PulseOffice.Sheets.Addressing.CellAddress(1, 0)] = new Cell { Value = PulseOffice.Sheets.Values.CellValue.Text("alfa") };
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
        var find = new LedgerFindTarget(context, Status(), texts, new SheetEditGuard(),
            new PulseOffice.Sheets.Engine.Operations.WorkbookSearch(), readOnly: false);

        find.ReplaceAll(new PulseOffice.UI.Controls.Search.FindRequest("alfa", "beta", new HashSet<string>()));

        Assert.Equal("Replace all", history.UndoDescription);
    }

    private sealed class WebAllowed : PulseOffice.Settings.ISettingsService
    {
        public IReadOnlyCollection<PulseOffice.Settings.SettingDefinition> Definitions => [];
        public PulseOffice.Settings.SettingDefinition? Find(string key) => null;
        public T Get<T>(string key) => (T)(object)true;
        public PulseOffice.Settings.SettingScope EffectiveScope(string key) => PulseOffice.Settings.SettingScope.User;
        public bool IsLocked(string key) => false;
        public void Set(string key, object? value, PulseOffice.Settings.SettingScope scope = PulseOffice.Settings.SettingScope.User) { }
        public void Reset(string key, PulseOffice.Settings.SettingScope scope = PulseOffice.Settings.SettingScope.User) { }
        public IEnumerable<PulseOffice.Settings.SettingDefinition> Search(string query) => [];
        public event EventHandler<PulseOffice.Settings.SettingChangedEventArgs>? Changed { add { } remove { } }
    }

    private sealed class StreamHandler(Stream body) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(System.Net.HttpStatusCode.OK) { Content = new StreamContent(body) };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class CountingStream(long length) : Stream
    {
        public long Served { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => Served; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var served = (int)Math.Min(count, length - Served);
            Array.Fill(buffer, (byte)'a', offset, served);
            Served += served;
            return served;
        }
    }

    private static PulseOffice.UI.Controls.Services.StatusBarService Status() => new(
        TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
        new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));

    private static SheetTarget Target(Workbook workbook, PulseOffice.Sheets.Engine.Recalculation.CalculationEngine engine, PulseOffice.Document.Commands.UndoStack history) => new()
    {
        Workbook = workbook,
        SheetIndex = 0,
        History = history,
        Engine = engine,
        Selection = () => PulseOffice.Sheets.Addressing.CellRange.Single(PulseOffice.Sheets.Addressing.CellAddress.First),
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
    };
}
