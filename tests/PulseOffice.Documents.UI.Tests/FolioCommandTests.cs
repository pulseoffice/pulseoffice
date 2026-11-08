using System.Text.Json;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Pages;
using PulseOffice.Text;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class NoMemory : PulseOffice.UI.Controls.Services.IRibbonMemory
{
    public string? Recall(string key, string? fallback) => fallback;

    public void Remember(string key, string? value)
    {
    }
}

public sealed class FolioCommandTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static IActiveFolioContext Active(FolioScene scene) => scene.Services.GetRequiredService<IActiveFolioContext>();

    private static IReadOnlyList<CommandDefinition> All(FolioScene scene)
    {
        var active = Active(scene);
        var services = scene.Services;
        return
        [
            .. services.GetRequiredService<FolioEditCommands>().GetCommands(),
            .. new FolioCharacterCommands(active, new TextCases()).GetCommands(),
            .. new FolioParagraphCommands(active).GetCommands(),
            .. new FolioTableCommands(active, services.GetRequiredService<FolioTableEditing>(), null!, null!, services.GetRequiredService<IFolioTableLooks>(), new PulseOffice.Document.Tables.TableStylePresets(), null!, null!, null!).GetCommands(),
            .. new FolioPageCommands(active, new FolioPageStyles(new PulseOffice.Documents.Blocks.BlockTree()), null!, null!, null!).GetCommands(),
            .. new FolioInsertCommands(active, null!, null!, null!, null!, null!, null!, null!, null!, null!, new PulseOffice.Foundation.Identity.SystemUserIdentity()).GetCommands(),
            .. new FolioFindCommands(active, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioDialogCommands(active, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioRibbonCommands(active, null!, null!, new NoMemory(), null!, null!, null!, null!, null!).GetCommands(),
            .. services.GetRequiredService<FolioZoomCommands>().GetCommands(),
            .. new FolioObjectCommands(active, services.GetRequiredService<IFolioObjectEditing>(), services.GetRequiredService<IFolioObjectMover>(), null!, services.GetRequiredService<PulseOffice.Documents.Editing.ICaptions>(), services.GetRequiredService<PulseOffice.Documents.Objects.IFrameChains>(), services.GetRequiredService<IFolioObjectCycle>()).GetCommands(),
            .. new FolioListCommands(active, services.GetRequiredService<PulseOffice.Documents.Editing.IListEditing>(), null!, null!, null!).GetCommands(),
            .. new FolioStyleCommands(active, null!, null!, null!, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioSectionCommands(active, services.GetRequiredService<PulseOffice.Documents.Editing.ISectionEditing>(), null!, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioViewCommands(active, services.GetRequiredService<FolioViewCommandServices>()).GetCommands(),
            .. new FolioToolCommands(active, services.GetRequiredService<FolioToolServices>()).GetCommands(),
            .. new FolioClipboardCommands(active, services.GetRequiredService<PulseOffice.Exchange.IObjectExchange>(), services.GetRequiredService<PulseOffice.Documents.Editing.Tables.ITablePasting>(), null!).GetCommands(),
            .. new FolioBreakCommands(active, null!, null!, null!, null!).GetCommands(),
            .. new FolioNavigationCommands(active, null!, null!, null!).GetCommands(),
            .. new FolioTextToolCommands(active, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioShapeCommands(active, null!).GetCommands(),
            .. new FolioSendCommands(active, null!).GetCommands(),
            .. new FolioAccessibilityCommands(active, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioAutoTextCommands(active, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioDrawCommands(active, services.GetRequiredService<IFolioDrawingTools>()).GetCommands(),
            .. new FolioCellCommands(active, services.GetRequiredService<PulseOffice.Documents.Editing.Tables.ICellNumberFormatting>(), services.GetRequiredService<IFolioNumberRecognition>(), null!, null!).GetCommands(),
            .. new FolioMasterCommands(active, services.GetRequiredService<PulseOffice.Documents.Editing.ISubdocuments>(), null!, null!, null!, null!, null!, null!, null!, null!, null!, null!).GetCommands(),
            .. new PulseOffice.Documents.UI.Scripting.FolioMacroCommands(active, null!, null!, null!, null!, null!, null!).GetCommands(),
            .. new FolioContentControlCommands(active, services.GetRequiredService<IFolioContentControls>()).GetCommands(),
        ];
    }

    private static void Run(FolioScene scene, string id) =>
        All(scene).Single(command => command.Id.Value == id).Execute(Context, CancellationToken.None).GetAwaiter().GetResult();

    private static bool Checked(FolioScene scene, string id) =>
        All(scene).Single(command => command.Id.Value == id).CheckedWhen?.Invoke(Context) == true;

    private static bool Enabled(FolioScene scene, string id) =>
        All(scene).Single(command => command.Id.Value == id).EnabledWhen?.Invoke(Context) != false;

    [Fact]
    public Task BarratoSiAccendeESiAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Alfa beta");
        scene.Editor.SelectAll();

        Run(scene, "folio.format.strikethrough");
        Assert.True(Checked(scene, "folio.format.strikethrough"));

        scene.History.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.False(Checked(scene, "folio.format.strikethrough"));
    }, CancellationToken.None);

    [Fact]
    public Task MaiuscoloTrasformaLaParolaSottoIlCursore() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno due");
        scene.Editor.PlaceCaret(scene.Editor.Caret with { Offset = 1 }, false);

        Run(scene, "folio.case.upper");

        Assert.Equal(["UNO due"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task AllineamentoEElencoSeguonoIlParagrafo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");

        Run(scene, "folio.paragraph.alignCenter");
        Run(scene, "folio.paragraph.numbering");

        Assert.True(Checked(scene, "folio.paragraph.alignCenter"));
        Assert.False(Checked(scene, "folio.paragraph.alignLeft"));
        Assert.True(Checked(scene, "folio.paragraph.numbering"));
        Assert.Equal(DefaultStyleNames.Numbering123, scene.Editor.CurrentParagraph().ListStyleName);

        Run(scene, "folio.paragraph.indentMore");
        Assert.Equal(1, scene.Editor.CurrentParagraph().BulletLevel);

        Run(scene, "folio.paragraph.numbering");
        Assert.Null(scene.Editor.CurrentParagraph().ListStyleName);
    }, CancellationToken.None);

    [Fact]
    public Task SolaLetturaSpegneIComandiDiModifica() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        scene.ReadOnly = true;

        Assert.False(Enabled(scene, "folio.format.strikethrough"));
        Assert.False(Enabled(scene, "folio.paragraph.justify"));
        Assert.False(Enabled(scene, "folio.page.landscape"));
    }, CancellationToken.None);

    [Fact]
    public Task OrientamentoDellaPaginaSiAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");

        Run(scene, "folio.page.landscape");
        Assert.True(Checked(scene, "folio.page.landscape"));
        Assert.True(scene.Editor.Layout.Pages[0].Width > scene.Editor.Layout.Pages[0].Height);

        scene.History.Undo();
        Assert.True(Checked(scene, "folio.page.portrait"));
    }, CancellationToken.None);

    [Fact]
    public Task IComandiDiTabellaValgonoSoloInUnaCella() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        Assert.False(Enabled(scene, "folio.table.rowBelow"));

        scene.Editor.Insert((documents, at) => documents.InsertTable(scene.Document, scene.History, at, 2, 2), "test");
        var table = scene.Document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Rows[0].Cells[0].Content.Blocks.OfType<ParagraphBlock>().Single();
        scene.Editor.PlaceCaret(new DocumentPosition(cell.Id, 0), false);

        Assert.True(Enabled(scene, "folio.table.rowBelow"));
        Run(scene, "folio.table.rowBelow");
        Run(scene, "folio.table.columnRight");
        Assert.Equal(3, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(3, row.Cells.Count));

        scene.History.Undo();
        scene.History.Undo();
        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(2, row.Cells.Count));
    }, CancellationToken.None);

    [Fact]
    public Task OgniComandoHaTitoloNelleCinqueLingueEIcona() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var ids = All(scene).Select(command => command.Id.Value).ToList();
        var icons = new PulseOffice.UI.Icons.FolioIcons();
        var resources = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PulseOffice.Localization", "Resources");
        foreach (var language in new[] { "it", "en", "de", "es", "fr" })
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, language + ".json")));
            var missing = ids.Where(id => !json.RootElement.TryGetProperty($"command.{id}.title", out _)).ToList();
            Assert.True(missing.Count == 0, $"{language}: {string.Join(", ", missing)}");
        }
        var withoutIcon = ids.Where(id => !id.StartsWith("folio.insert.character.", StringComparison.Ordinal) && !icons.Contains(id)).ToList();
        Assert.True(withoutIcon.Count == 0, string.Join(", ", withoutIcon));
        var gestures = All(scene).Where(command => command.DefaultGesture is not null).GroupBy(command => command.DefaultGesture).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        Assert.Empty(gestures);
    }, CancellationToken.None);

    [Fact]
    public Task UnOggettoSiSelezionaSiRidimensionaESiElimina() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(3));
        var image = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "img", Data = new byte[] { 1 }, MediaType = "image/png", NaturalSize = size };
        scene.Editor.Insert((documents, at) => documents.InsertObject(scene.Document, scene.History,
            new PulseOffice.Documents.Objects.AnchoredObject("o1", new PulseOffice.Documents.Objects.AsCharacterAnchor(at.ParagraphId), new PulseOffice.Documents.Objects.EmbeddedContent(image), size), at), "test");

        Assert.False(Enabled(scene, "folio.object.delete"));
        scene.Editor.SelectObject("o1");
        Assert.True(Enabled(scene, "folio.object.delete"));
        Assert.Equal(FolioContextMenus.ObjectRoot, new FolioContextMenus(null!, scene.Services.GetRequiredService<ITableEditing>(), new PulseOffice.UI.Controls.Services.ContextMenuKey()).RootFor(scene.Editor));

        var bigger = size with { Width = PulseOffice.Foundation.Units.Twip.FromCentimeters(8) };
        Assert.True(scene.Editor.ResizeSelectedObject(bigger));
        Assert.True(Enabled(scene, "folio.object.originalSize"));
        Run(scene, "folio.object.originalSize");
        Assert.Equal(size, scene.Document.Objects.Find("o1")!.Size);

        scene.Press(Avalonia.Input.Key.Delete);
        Assert.Null(scene.Document.Objects.Find("o1"));
        Assert.Null(scene.Editor.SelectedObjectId);
        Assert.DoesNotContain(scene.Document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks), mark => mark is PulseOffice.Documents.Marks.ObjectMark);

        scene.History.Undo();
        Assert.NotNull(scene.Document.Objects.Find("o1"));
        Assert.Contains(scene.Document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks), mark => mark is PulseOffice.Documents.Marks.ObjectMark);
    }, CancellationToken.None);

    [Fact]
    public Task LInterruzioneOrizzontaleApreUnaPaginaLandscape() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("prima");

        Run(scene, "folio.insert.landscapeBreak");
        scene.Type("dopo");

        var blocks = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(DefaultStyleNames.Landscape, blocks[1].Content.PageStyleName);
        Assert.Equal(2, scene.Editor.Layout.Pages.Count);
        Assert.True(scene.Editor.Layout.Pages[1].Width > scene.Editor.Layout.Pages[1].Height);
    }, CancellationToken.None);

    [Fact]
    public Task LeRigheCopiateSiIncollanoSopraNellAltraTabella() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Editor.Insert((writer, at) => writer.InsertTable(scene.Document, scene.History, at, 2, 2), "table");
        var table = scene.Document.Body.Blocks.OfType<TableBlock>().Single();
        var first = (ParagraphBlock)table.Rows[0].Cells[0].Content.Blocks[0];
        scene.Editor.Select(new DocumentPosition(first.Id, 0), new DocumentPosition(first.Id, 0));
        scene.Type("a1");
        var fragment = new PulseOffice.Documents.Clipboard.DocumentFragment(new BlockContainer([table.Clone()]), scene.Document.Styles, [], [], []);
        _ = scene.Services.GetRequiredService<PulseOffice.Exchange.IObjectExchange>().PublishAsync(new PulseOffice.Exchange.ExchangePackage { Id = "tabella", Origin = PulseOffice.Foundation.Modules.ModuleKind.Documents, Native = new PulseOffice.Documents.UI.Exchange.FolioNative(fragment) });
        var last = (ParagraphBlock)table.Rows[1].Cells[1].Content.Blocks[0];
        scene.Editor.Select(new DocumentPosition(last.Id, 0), new DocumentPosition(last.Id, 0));

        Assert.True(Enabled(scene, "folio.clipboard.pasteRowsBefore"));
        Run(scene, "folio.clipboard.pasteRowsBefore");

        Assert.Equal(4, table.Rows.Count);
        Assert.Equal("a1", ((ParagraphBlock)table.Rows[1].Cells[0].Content.Blocks[0]).Text);
        var ids = scene.Services.GetRequiredService<IStoryCatalog>().Paragraphs(scene.Document).Select(paragraph => paragraph.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        Run(scene, "folio.clipboard.pasteColumnsBefore");
        Assert.Equal(4, table.Rows[0].Cells.Count);
        Assert.Equal(4, table.Columns.Count);
    }, CancellationToken.None);

    [Fact]
    public Task IlFormatoDeiCommentiValePerTutti() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("testo");
        var id = scene.Editor.Caret.ParagraphId;
        scene.Editor.Change(() => scene.Services.GetRequiredService<IDocumentEditor>().AddComment(scene.Document, scene.History, DocumentRange.In(id, 0, 5), "Ada", DateTimeOffset.UnixEpoch, "nota"));

        Run(scene, "folio.format.comments");

        var paragraph = (ParagraphBlock)scene.Document.Comments.All.Single().Content.Blocks[0];
        Assert.All(paragraph.Content.Runs, run => Assert.Equal(9, run.FontSize));
    }, CancellationToken.None);

    [Fact]
    public Task IlNumeroDiPaginaVaNelPieDiPagina() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();

        Run(scene, "folio.insert.pageNumberWizard");

        var style = scene.Document.Styles.Page.Find(scene.Document.InitialPageStyle)!;
        Assert.True(style.Footer.Enabled);
        var paragraph = style.Footer.Default.Blocks.OfType<ParagraphBlock>().Last();
        Assert.Contains(paragraph.Content.Runs, run => run.Field == TextFieldKind.PageNumber);
        Assert.Equal(TextAlign.Center, paragraph.Content.Align);

        scene.History.Undo();
        Assert.False(scene.Document.Styles.Page.Find(scene.Document.InitialPageStyle)!.Footer.Enabled);
    }, CancellationToken.None);

    [Fact]
    public Task CalcolaLaSelezioneMetteIlRisultatoNegliAppunti() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("2+3*4");
        scene.Editor.SelectAll();

        Run(scene, "folio.tools.calculate");

        Assert.Equal("14", scene.Clipboard.Text);
    }, CancellationToken.None);

    [Fact]
    public Task LaLineaOrizzontaleCreaLoStileELoApplica() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();

        Run(scene, "folio.style.horizontalLine");

        Assert.Equal("Horizontal Line", scene.Document.Body.Blocks.OfType<ParagraphBlock>().First().StyleName);
        Assert.NotNull(scene.Document.Styles.Paragraph.Find("Horizontal Line")?.Paragraph.Borders?.Bottom);
        scene.History.Undo();
        Assert.Null(scene.Document.Styles.Paragraph.Find("Horizontal Line"));
    }, CancellationToken.None);
}

public sealed class FolioTableCommandTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.Table, DocumentState.DocumentOpen);

    private static IReadOnlyList<CommandDefinition> Commands(FolioScene scene) =>
        [.. new FolioTableCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), scene.Services.GetRequiredService<FolioTableEditing>(), null!, null!,
            scene.Services.GetRequiredService<IFolioTableLooks>(), new PulseOffice.Document.Tables.TableStylePresets(), null!, null!, null!).GetCommands()];

    private static CommandDefinition Command(FolioScene scene, string id) => Commands(scene).Single(command => command.Id.Value == id);

    private static TableBlock Table(FolioScene scene, int rows, int columns)
    {
        scene.Editor.Insert((documents, at) => documents.InsertTable(scene.Document, scene.History, at, rows, columns), "insert-table");
        return scene.Document.Body.Blocks.OfType<TableBlock>().Single();
    }

    private static ParagraphBlock Paragraph(TableBlock table, int row, int column) => table.Cell(row, column).Content.Blocks.OfType<ParagraphBlock>().First();

    [Fact]
    public Task IComandiDiTabellaSiAccendonoSoloNellaTabella() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Prima");
        Assert.False(Command(scene, "folio.table.merge").IsEnabled(Context));
        Assert.True(Command(scene, "folio.table.textToTable").EnabledWhen!(Context));

        var table = Table(scene, 2, 2);
        scene.Editor.Select(new DocumentPosition(Paragraph(table, 0, 0).Id, 0), new DocumentPosition(Paragraph(table, 0, 0).Id, 0));
        Assert.True(Command(scene, "folio.table.properties").IsEnabled(Context));
        Assert.False(Command(scene, "folio.table.merge").IsEnabled(Context));
        Assert.False(Command(scene, "folio.table.textToTable").EnabledWhen!(Context));

        scene.ReadOnly = true;
        Assert.False(Command(scene, "folio.table.properties").IsEnabled(Context));
    }, CancellationToken.None);

    [Fact]
    public Task SelezionareUnaColonnaEUnirlaCreaUnaCellaAlta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var table = Table(scene, 3, 2);
        scene.Editor.Select(new DocumentPosition(Paragraph(table, 1, 1).Id, 0), new DocumentPosition(Paragraph(table, 1, 1).Id, 0));

        Command(scene, "folio.table.selectColumn").Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(new PulseOffice.Document.Tables.TableCellRange(0, 1, 2, 1), scene.Editor.CellRange()!.Range);
        Command(scene, "folio.table.merge").Execute(Context, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(3, table.Cell(0, 1).RowSpan);
        scene.History.Undo();
        Assert.Equal(1, table.Cell(0, 1).RowSpan);
    }, CancellationToken.None);

    [Fact]
    public Task TabPassaAllaCellaDopoEDAllUltimaAggiungeUnaRiga() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var table = Table(scene, 1, 2);
        scene.Editor.Select(new DocumentPosition(Paragraph(table, 0, 0).Id, 0), new DocumentPosition(Paragraph(table, 0, 0).Id, 0));

        Assert.True(scene.Editor.MoveToCell(false));
        Assert.Equal(Paragraph(table, 0, 1).Id, scene.Editor.Caret.ParagraphId);
        Assert.True(scene.Editor.MoveToCell(false));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(Paragraph(table, 1, 0).Id, scene.Editor.Caret.ParagraphId);
        Assert.True(scene.Editor.MoveToCell(true));
        Assert.Equal(Paragraph(table, 0, 1).Id, scene.Editor.Caret.ParagraphId);
    }, CancellationToken.None);

    [Fact]
    public Task ScrivereInUnaCellaRicalcolaLaFormulaInUnSoloPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Document.Language = "it-IT";
        var table = Table(scene, 3, 1);
        var editing = scene.Services.GetRequiredService<FolioTableEditing>();
        var at = editing.Tables.Locate(scene.Document, Paragraph(table, 2, 0).Id)!;
        editing.Formulas.SetFormula(scene.Document, scene.History, at, "=SUM(ABOVE)", scene.Editor.Culture());
        scene.Editor.Select(new DocumentPosition(Paragraph(table, 0, 0).Id, 0), new DocumentPosition(Paragraph(table, 0, 0).Id, 0));

        scene.Editor.Type("4");

        Assert.Equal("4", editing.Grid.PlainText(table.Cell(2, 0)));
        scene.History.Undo();
        Assert.Equal("0", editing.Grid.PlainText(table.Cell(2, 0)));
    }, CancellationToken.None);
}
