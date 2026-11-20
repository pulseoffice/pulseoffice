using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round287AspectLockTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static EmbeddedObject FreePicture() =>
        new RasterImageObject { Id = "foto", Data = Png, MediaType = "image/png" }.WithAspectLock(false);

    private static EmbeddedObject LockedShape() =>
        new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle }.WithAspectLock(true);

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "foto", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)), Content = FreePicture() });
        slide.Add(new ObjectElement { Id = "forma", Bounds = new DocumentRect(new Twip(5000), new Twip(1000), new Twip(3000), new Twip(2000)), Content = LockedShape() });
        deck.AddExisting(slide);
        return deck;
    }

    private static Workbook Book()
    {
        var workbook = Workbook.CreateDefault();
        var size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3));
        workbook.Sheets[0].Objects.Add(new PlacedObject { Id = "foto", Content = FreePicture(), Anchor = new CellAddress(2, 1), Size = size });
        workbook.Sheets[0].Objects.Add(new PlacedObject { Id = "forma", Content = LockedShape(), Anchor = new CellAddress(8, 1), Size = size });
        return workbook;
    }

    private static TextDocument Text()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        var size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(FreePicture()), size));
        document.Objects.Add(new AnchoredObject("shp", new ParagraphAnchor("p1"), new EmbeddedContent(LockedShape()), size));
        return document;
    }

    private static void AssertLocks(IEnumerable<EmbeddedObject> contents)
    {
        var list = contents.ToList();
        Assert.False(Assert.Single(list.OfType<RasterImageObject>()).LocksAspectRatio);
        Assert.True(Assert.Single(list.OfType<ShapeObject>()).LocksAspectRatio);
    }

    private static IEnumerable<EmbeddedObject> Contents(Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content);

    private static IEnumerable<EmbeddedObject> Contents(TextDocument document) =>
        document.Objects.All.Select(item => item.Content).OfType<EmbeddedContent>().Select(content => content.Value);

    [Fact]
    public void Defaults_lock_pictures_and_free_shapes()
    {
        Assert.True(new RasterImageObject { Id = "p", Data = Png, MediaType = "image/png" }.LocksAspectRatio);
        Assert.False(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }.LocksAspectRatio);
        Assert.Null(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }.WithAspectLock(false).AspectLock);
    }

    [Fact]
    public void The_lock_crosses_pptx_and_odp()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), pptx).Succeeded);
        pptx.Position = 0;
        AssertLocks(Contents(new PptxReader(_themes, _masters).Read(pptx).Value!));

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), odp).Succeeded);
        odp.Position = 0;
        AssertLocks(Contents(new OdpReader(_themes, _masters).Read(odp).Value!));
    }

    [Fact]
    public void The_lock_crosses_xlsx_and_ods()
    {
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(Book(), xlsx);
        xlsx.Position = 0;
        AssertLocks(new XlsxReader().Read(xlsx).Value!.Sheets[0].Objects.Select(placed => placed.Content));

        using var ods = new MemoryStream();
        new OdsWriter().Write(Book(), ods);
        ods.Position = 0;
        AssertLocks(new OdsReader().Read(ods).Value!.Sheets[0].Objects.Select(placed => placed.Content));
    }

    [Fact]
    public void The_lock_crosses_docx_and_odt()
    {
        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Text(), docx).Succeeded);
        docx.Position = 0;
        AssertLocks(Contents(_composer.Reader(DocxFlavour.Document).Read(docx).Value!));

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(Text(), odt).Succeeded);
        odt.Position = 0;
        AssertLocks(Contents(new OdtReader().Read(odt).Value!));
    }
}
