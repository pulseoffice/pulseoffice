using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Units;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ContextRefusalTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-refusal-" + Guid.NewGuid().ToString("N"));

    public ContextRefusalTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Suite(ServiceProvider provider, CommandRegistry registry, ShellWindow window) : IAsyncDisposable
    {
        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public ShellWindow Window => window;

        public IDocumentWorkspace Workspace => Service<IDocumentWorkspace>();

        public async Task<CommandOutcome> Run(string id)
        {
            var outcome = await Service<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId(id))!, Workspace.Context);
            Dispatcher.UIThread.RunJobs();
            return outcome;
        }

        public bool Enabled(string id) => registry.Find(new CommandId(id))!.IsEnabled(Workspace.Context);

        public void Type(string text)
        {
            window.KeyTextInput(text);
            Dispatcher.UIThread.RunJobs();
        }

        public void Press(Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            window.KeyPress(key, modifiers, physical, null);
            window.KeyRelease(key, modifiers, physical, null);
            Dispatcher.UIThread.RunJobs();
        }

        public ValueTask DisposeAsync()
        {
            window.Close();
            return provider.DisposeAsync();
        }
    }

    private static async Task<Suite> Open(string module)
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var suite = new Suite(provider, registry, window);
        await suite.Run($"shell.file.new.{module}");
        ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)suite.Workspace.Active!.View).FocusDocument();
        Dispatcher.UIThread.RunJobs();
        return suite;
    }

    [Fact]
    public Task Su_un_foglio_protetto_maiuscole_e_taglia_rifiutano_senza_toccare_gli_appunti() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open("sheets");
        ledger.Type("Testo");
        ledger.Press(Key.Enter, PhysicalKey.Enter);
        ledger.Press(Key.Up, PhysicalKey.ArrowUp);
        var sheet = ledger.Service<PulseOffice.Sheets.UI.IActiveSheetContext>();
        var worksheet = sheet.Workbook!.Sheets[sheet.SheetIndex];
        worksheet.Protection = worksheet.Protection with { IsEnabled = true };
        var exchange = ledger.Service<PulseOffice.Exchange.IObjectExchange>();
        var before = exchange.Last;

        var lower = await ledger.Run("ledger.text.lower");
        var cut = await ledger.Run("ledger.clipboard.cut");

        foreach (var id in new[] { "ledger.sheet.insertRowsAbove", "ledger.sheet.deleteColumns", "ledger.shape.rectangle", "ledger.insert.currentDate", "ledger.data.autoFilter" })
            Assert.True((await ledger.Run(id)).Kind == CommandOutcomeKind.Refused, id);
        Assert.Empty(worksheet.Objects);
        Assert.Equal(CommandOutcomeKind.Refused, lower.Kind);
        Assert.Equal(CommandOutcomeKind.Refused, cut.Kind);
        Assert.Same(before, exchange.Last);
        Assert.Equal("Testo", worksheet.GetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0)).AsText());
    }, CancellationToken.None);

    [Fact]
    public Task Annullare_dopo_aver_protetto_il_foglio_non_cambia_le_celle_bloccate() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open("sheets");
        ledger.Type("Testo");
        ledger.Press(Key.Enter, PhysicalKey.Enter);
        var sheet = ledger.Service<PulseOffice.Sheets.UI.IActiveSheetContext>();
        var worksheet = sheet.Workbook!.Sheets[sheet.SheetIndex];
        worksheet.Protection = worksheet.Protection with { IsEnabled = true };

        await ledger.Run("shell.edit.undo");

        Assert.Equal("Testo", worksheet.GetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0)).AsText());
        Assert.True(sheet.History!.CanUndo);
        await ledger.Run("shell.edit.undo");
        Assert.Equal("Testo", worksheet.GetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0)).AsText());
    }, CancellationToken.None);

    [Fact]
    public Task Annullare_dopo_aver_protetto_il_documento_non_cambia_il_testo() => Session.Dispatch(async () =>
    {
        await using var folio = await Open("documents");
        folio.Type("Riservato");
        var editor = folio.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
        editor.Document.Protection = new PulseOffice.Documents.Protection.DocumentProtection(PulseOffice.Documents.Protection.DocumentRestriction.Comments, null);

        await folio.Run("shell.edit.undo");

        Assert.Equal("Riservato", editor.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Text);
        Assert.True(editor.History.CanUndo);
    }, CancellationToken.None);

    [Fact]
    public Task In_una_sezione_protetta_scrivere_e_cambiare_elenco_si_rifiutano_con_un_motivo() => Session.Dispatch(async () =>
    {
        await using var folio = await Open("documents");
        folio.Type("Riservato");
        var editor = folio.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
        var sections = folio.Service<PulseOffice.Documents.Editing.ISectionEditing>();
        editor.Change(() => sections.Wrap(editor.Document, editor.History, editor.ParagraphRange(),
            new PulseOffice.Documents.Editing.SectionSettings("Riservata", PulseOffice.Documents.Blocks.SectionColumns.Single, true, false)));
        var history = folio.Service<PulseOffice.UI.Controls.Notifications.INotificationHistory>();
        var text = editor.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.SectionBlock>().Single().Content.Blocks
            .OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Single();

        folio.Type("x");
        var typed = history.Entries[0].Notification.Message;
        var list = await folio.Run("folio.list.style.number");

        var blocked = folio.Service<PulseOffice.Localization.ITextCatalog>()["folio.protect.blocked"];
        Assert.Equal(blocked, typed);
        Assert.Equal(CommandOutcomeKind.Refused, list.Kind);
        Assert.Equal(blocked, list.Reason);
        Assert.Equal("Riservato", text.Text);
        Assert.Null(editor.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.SectionBlock>().Single().Content.Blocks
            .OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Single().Content.ListStyleName);
    }, CancellationToken.None);

    [Fact]
    public Task Con_il_documento_protetto_per_i_soli_commenti_i_comandi_che_scrivono_coi_servizi_si_annullano() => Session.Dispatch(async () =>
    {
        await using var folio = await Open("documents");
        folio.Type("Riservato");
        var editor = folio.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
        editor.Document.Protection = new PulseOffice.Documents.Protection.DocumentProtection(PulseOffice.Documents.Protection.DocumentRestriction.Comments, null);
        var blocks = editor.Document.Body.Count;

        var list = await folio.Run("folio.list.style.number");

        Assert.Equal(CommandOutcomeKind.Refused, list.Kind);
        Assert.Equal(blocks, editor.Document.Body.Count);
        var paragraph = editor.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First();
        Assert.Equal("Riservato", paragraph.Text);
        Assert.Null(paragraph.Content.ListStyleName);
    }, CancellationToken.None);

    [Fact]
    public Task In_una_cella_protetta_cambiare_elenco_si_rifiuta() => Session.Dispatch(async () =>
    {
        await using var folio = await Open("documents");
        var editor = folio.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
        var inner = new PulseOffice.Documents.Blocks.ParagraphBlock("cella-protetta", "Cella");
        var cell = new PulseOffice.Documents.Blocks.TableCell(new PulseOffice.Documents.Blocks.BlockContainer([inner])) { Protected = true };
        var tableBlock = new PulseOffice.Documents.Blocks.TableBlock("tabella-protetta",
            [new PulseOffice.Documents.Blocks.TableRow([cell])], [Twip.FromCentimeters(5)]);
        editor.Document.Body.Insert(0, tableBlock);
        editor.Reflow();
        editor.Select(new PulseOffice.Documents.Editing.DocumentPosition(inner.Id, 0), new PulseOffice.Documents.Editing.DocumentPosition(inner.Id, 2));

        var list = await folio.Run("folio.list.style.number");

        Assert.Equal(CommandOutcomeKind.Refused, list.Kind);
        Assert.Null(inner.Content.ListStyleName);
    }, CancellationToken.None);

    [Fact]
    public Task Il_glossario_senza_parola_davanti_al_cursore_lo_dice()=> Session.Dispatch(async () =>
    {
        await using var folio = await Open("documents");

        var outcome = await folio.Run("folio.tools.autoTextExpand");

        Assert.Equal(CommandOutcomeKind.Refused, outcome.Kind);
        Assert.Equal(folio.Service<PulseOffice.Localization.ITextCatalog>()["folio.autotext.noWord"], outcome.Reason);
    }, CancellationToken.None);

    [Fact]
    public Task Una_cornice_piu_alta_della_riga_si_centra_sopra_e_sotto_la_riga() => Session.Dispatch(async () =>
    {
        await using var folio = await Open("documents");
        folio.Type("Riga");
        var editor = folio.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
        var objects = folio.Service<PulseOffice.Documents.UI.Editing.IFolioObjectEditing>();
        objects.InsertTextFrame(editor, new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)));
        Dispatcher.UIThread.RunJobs();
        Assert.False(folio.Enabled("folio.object.selectNext"));
        await folio.Run("folio.object.anchor.paragraph");

        await folio.Run("folio.object.alignTop");
        var top = objects.Selected(editor)!.Placement.Y;
        await folio.Run("folio.object.alignMiddle");
        var middle = objects.Selected(editor)!.Placement.Y;

        Assert.True(middle.ToPoints() < top.ToPoints(), $"middle {middle} should rise above the top of the line {top}");
    }, CancellationToken.None);

    [Fact]
    public Task In_Deck_i_dialoghi_del_testo_lavorano_sul_testo_della_forma_e_la_forma_vuota_spegne_la_formattazione() => Session.Dispatch(async () =>
    {
        await using var deck = await Open("presentations");
        var target = deck.Service<PulseOffice.Presentations.UI.IActiveDeckContext>().Target!;
        var slide = target.CurrentSlide()!;
        var element = new PulseOffice.Presentations.ObjectElement
        {
            Id = target.Deck.NextId(),
            Bounds = new DocumentRect(Twip.FromCentimeters(3), Twip.FromCentimeters(3), Twip.FromCentimeters(6), Twip.FromCentimeters(3)),
            ZOrder = slide.Elements.Count,
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = target.Deck.NextId(), Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
        };
        target.Actions.InsertElement(slide, element);
        target.Select([element]);
        Dispatcher.UIThread.RunJobs();
        Assert.False(deck.Enabled("deck.format.italic"));

        deck.Type("Forma");
        Assert.True(target.IsTyping());
        var host = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(deck.Window).OfType<PulseOffice.UI.Controls.DialogHost>().Single();
        var running = deck.Run("deck.format.paragraph");
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.IsDialogOpen);
        host.Close(null);
        Dispatcher.UIThread.RunJobs();
        await running;
    }, CancellationToken.None);

    [Fact]
    public Task Chiudere_la_presentazione_ne_ferma_la_proiezione() => Session.Dispatch(async () =>
    {
        await using var deck = await Open("presentations");
        var remote = deck.Service<PulseOffice.Presentations.Remote.IRemoteSession>();

        await deck.Run("deck.present");
        Assert.NotNull(remote.Target);
        deck.Workspace.Close(deck.Workspace.Active!);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(remote.Target);
    }, CancellationToken.None);
}
