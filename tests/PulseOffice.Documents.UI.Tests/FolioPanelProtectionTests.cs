using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Indexes;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPanelProtectionTests
{
    private static void Protect(FolioScene scene) => scene.Document.Protection = new DocumentProtection(DocumentRestriction.Comments, null);

    [Fact]
    public Task IMarginiDelRighelloNonCambianoNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var pages = scene.Services.GetRequiredService<IFolioPageStyles>();
        var before = pages.Read(pages.Current(scene.Editor)).Margins;
        Protect(scene);

        Assert.False(pages.Change(scene.Editor, geometry => geometry with { Margins = geometry.Margins with { Left = Twip.FromPoints(10) } }, "ruler-margins"));

        Assert.Equal(before, pages.Read(pages.Current(scene.Editor)).Margins);
    }, CancellationToken.None);

    [Fact]
    public Task IlPannelloStiliNonCambiaNullaNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        scene.Services.GetRequiredService<IStyleEditing>().Add(scene.Document, scene.History, scene.Document.Styles.Paragraph, new ParagraphStyle("Mio stile"), "test");
        var page = scene.Document.Styles.Page.All.First(style => style.Name != scene.Services.GetRequiredService<IFolioPageStyles>().Current(scene.Editor).Name).Name;
        var services = scene.Services;
        var actions = new FolioStyleActions(services.GetRequiredService<IStyleEditing>(), services.GetRequiredService<IStyleFromSelection>(), null!,
            services.GetRequiredService<IFolioStyleNames>(), services.GetRequiredService<IFolioPageStyles>(), services.GetRequiredService<PulseOffice.Documents.Editing.Tables.ITableFormatting>());
        Protect(scene);

        Assert.False(actions.CanApply(scene.Editor, StyleFamily.Page));
        actions.Apply(scene.Editor, StyleFamily.Page, page);
        actions.Delete(scene.Editor, StyleFamily.Paragraph, "Mio stile");

        Assert.NotEqual(page, scene.Services.GetRequiredService<IFolioPageStyles>().Current(scene.Editor).Name);
        Assert.True(scene.Document.Styles.Paragraph.Contains("Mio stile"));
    }, CancellationToken.None);

    [Fact]
    public Task IlPannelloVociDIndiceNonScriveNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Parola");
        Protect(scene);

        Assert.False(scene.Services.GetRequiredService<IFolioIndexEntries>().Insert(scene.Editor, FolioIndexEntries.Alphabetical, "Parola", string.Empty));

        Assert.Empty(scene.Document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<IndexEntryMark>());
    }, CancellationToken.None);

    [Fact]
    public Task LeManiglieDiRegolazioneNonCambianoLaFormaNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Primo");
        var first = scene.Document.Body.Blocks.OfType<ParagraphBlock>().First();
        var size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3));
        var image = new RasterImageObject { Id = "img", Data = new byte[] { 1 }, MediaType = "image/png", NaturalSize = size };
        var item = new AnchoredObject("o1", new ParagraphAnchor(first.Id), new EmbeddedContent(image), size);
        scene.Editor.Insert((documents, _) => documents.InsertObject(scene.Document, scene.History, item, new DocumentPosition(first.Id, 0)), "test");
        scene.Editor.SelectObject("o1");
        Protect(scene);
        var editing = scene.Services.GetRequiredService<IFolioObjectEditing>();

        Assert.False(editing.Replace(scene.Editor, value => value is RasterImageObject raster ? raster with { MediaType = "image/jpeg" } : value));
        Assert.False(editing.Wrap(scene.Editor, WrapMode.None));

        Assert.Equal("image/png", ((RasterImageObject)((EmbeddedContent)scene.Document.Objects.Find("o1")!.Content).Value).MediaType);
    }, CancellationToken.None);

    [Fact]
    public Task TrascinareIlBordoDiColonnaNonCambiaLaTabellaProtetta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document =>
        {
            TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock(document.NextId("p"), text)]));
            document.Body.Replace([new TableBlock(document.NextId("t"), [new TableRow([Cell("A1"), Cell("B1")])], [Twip.FromCentimeters(5), Twip.FromCentimeters(5)]), new ParagraphBlock(document.NextId("p"), "Dopo")]);
        });
        var block = scene.Document.Body.Blocks.OfType<TableBlock>().First();
        var before = block.Columns[0];
        Protect(scene);

        Assert.False(scene.Editor.ResizeColumn(block, 0, before.ToPoints() + 40));

        Assert.Equal(before, scene.Document.Body.Blocks.OfType<TableBlock>().First().Columns[0]);
    }, CancellationToken.None);

    [Fact]
    public Task LeMacroNonRisolvonoLeRevisioniNeSpengonoLaRegistrazioneImposta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var doc = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Scripting.FolioFacade>();
        doc.SetTrackChanges(true);
        doc.InsertText("nuovo");
        var changes = doc.ChangeCount;
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.TrackedChanges, null);

        Assert.Throws<InvalidOperationException>(() => doc.AcceptAll());
        Assert.Throws<InvalidOperationException>(() => doc.RejectAll());
        Assert.Throws<InvalidOperationException>(() => doc.SetTrackChanges(false));

        Assert.Equal(changes, doc.ChangeCount);
        Assert.True(scene.Document.Changes.IsRecording);
    }, CancellationToken.None);

    [Fact]
    public Task IlCampoUtenteNonCambiaLeVariabiliNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        Protect(scene);
        var services = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Fields.FolioFieldsServices>();
        var model = new PulseOffice.Documents.UI.Fields.FolioFieldsModel(scene.Editor, services, new PulseOffice.Documents.UI.Fields.FolioFieldsStart(PulseOffice.Documents.UI.Fields.FolioFieldsPage.Fields));
        var user = services.Catalog.Choices.First(choice => choice is { Kind: PulseOffice.Text.Rich.TextFieldKind.Variable, Argument: "user" });

        Assert.False(model.Apply(user, new Dictionary<string, object> { ["name"] = "cliente", ["value"] = "Rossi" }));

        Assert.False(scene.Document.Variables.ContainsKey("cliente"));
    }, CancellationToken.None);

    [Fact]
    public Task LIstantaneaDiRipristinoConservaProtezioniEImpostazioni() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene(document =>
        {
            TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock(document.NextId("p"), text)]));
            var locked = Cell("A1");
            locked.Protected = true;
            locked.NumberFormat = new CellNumberFormat(CellValueKind.Currency) { Symbol = "€" };
            var table = new TableBlock(document.NextId("t"), [new TableRow([locked, Cell("B1")])], [Twip.FromCentimeters(5), Twip.FromCentimeters(5)])
            {
                PageBreakBefore = true,
                PageStyleName = "Landscape",
            };
            document.Body.Replace([table, new ParagraphBlock(document.NextId("p"), "Dopo")]);
        });
        var document = scene.Document;
        document.Marks = new MarkProtection(true, true);
        document.LineNumbering = new LineNumbering(10) { Side = LineNumberSide.Right };
        document.IsMaster = true;
        document.MergeSource = new PulseOffice.Documents.Fields.MailMergeSource("Rubrica", "Contatti", PulseOffice.Documents.Fields.MergeCommandType.Table);
        document.Styles.TabsRelativeToIndent = false;
        document.Styles.DefaultTabInterval = 30;
        document.Bibliography.Store(new PulseOffice.Documents.Fields.BibliographySource("Eco1980", "book", new Dictionary<string, string> { ["Title"] = "Il nome della rosa" }));
        var codec = scene.Services.GetRequiredService<PulseOffice.Documents.UI.Shell.IFolioSnapshotCodec>();
        using var stream = new MemoryStream();
        await codec.WriteAsync(document, stream, CancellationToken.None);
        stream.Position = 0;

        var restored = await codec.ReadAsync(stream, CancellationToken.None);

        Assert.NotNull(restored);
        var cell = restored.Body.Blocks.OfType<TableBlock>().Single().Cell(0, 0);
        Assert.True(cell.Protected);
        Assert.Equal(document.Body.Blocks.OfType<TableBlock>().Single().Cell(0, 0).NumberFormat, cell.NumberFormat);
        Assert.True(restored.Body.Blocks.OfType<TableBlock>().Single().PageBreakBefore);
        Assert.Equal("Landscape", restored.Body.Blocks.OfType<TableBlock>().Single().PageStyleName);
        Assert.Equal(document.Marks, restored.Marks);
        Assert.Equal(document.LineNumbering, restored.LineNumbering);
        Assert.True(restored.IsMaster);
        Assert.Equal(document.MergeSource, restored.MergeSource);
        Assert.False(restored.Styles.TabsRelativeToIndent);
        Assert.Equal(30, restored.Styles.DefaultTabInterval);
        Assert.Equal("Il nome della rosa", restored.Bibliography.Find("Eco1980")?.Value("Title"));
    });
}
