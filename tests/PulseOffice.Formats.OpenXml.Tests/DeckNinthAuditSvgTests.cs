using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;
using SVG = DocumentFormat.OpenXml.Office2019.Drawing.SVG;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckNinthAuditSvgTests
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><path d=\"M0 0H10V10Z\" fill=\"#ff0000\"/></svg>";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private sealed class MarkedRasterizer : IVectorRasterizer
    {
        public static readonly byte[] Marker = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x42];

        public int Calls { get; private set; }

        public byte[]? Png(byte[] svg, int pixelWidth, int pixelHeight)
        {
            Calls++;
            Assert.True(pixelWidth > 0 && pixelHeight > 0);
            return Marker;
        }
    }

    private Presentation Deck(bool flip = false)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "icon",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(2000)),
            Content = new VectorGraphicObject
            {
                Id = "icon",
                Asset = new VectorAsset { Id = "icon", Name = "icon", SourceKind = VectorSourceKind.InlineSvg, SvgContent = Svg },
                FlipHorizontal = flip,
            },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private VectorGraphicObject Vector(Presentation deck) =>
        Assert.IsType<VectorGraphicObject>(Assert.Single(deck.Slides[0].Elements.OfType<ObjectElement>()).Content);

    [Fact]
    public void Un_SVG_di_Deck_si_scrive_in_PPTX_come_svgBlip_con_PNG_di_riserva_e_si_rilegge_come_SVG()
    {
        var rasterizer = new MarkedRasterizer();
        var deck = Deck(flip: true);
        Assert.DoesNotContain(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX004");
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter(rasterizer).Write(deck, buffer).Succeeded);
        Assert.Equal(1, rasterizer.Calls);

        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, false))
        {
            var slide = document.PresentationPart!.SlideParts.First();
            var picture = Assert.Single(slide.Slide!.CommonSlideData!.ShapeTree!.Elements<P.Picture>());
            var blip = picture.BlipFill!.Blip!;
            var fallback = (ImagePart)slide.GetPartById(blip.Embed!.Value!);
            Assert.Equal("image/png", fallback.ContentType);
            using (var stream = fallback.GetStream())
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                Assert.Equal(MarkedRasterizer.Marker, copy.ToArray());
            }
            var vector = (ImagePart)slide.GetPartById(Assert.Single(blip.Descendants<SVG.SVGBlip>()).Embed!.Value!);
            Assert.Equal("image/svg+xml", vector.ContentType);
        }

        buffer.Position = 0;
        var again = Vector(new PptxReader(_themes, _masters).Read(buffer).Value!);
        Assert.Contains("M0 0H10V10Z", again.Asset.SvgContent, StringComparison.Ordinal);
        Assert.True(again.FlipHorizontal);
    }

    [Fact]
    public void Un_SVG_di_Deck_si_scrive_in_ODP_con_PNG_di_riserva_e_fa_il_giro_in_entrambi_i_formati()
    {
        var rasterizer = new MarkedRasterizer();
        var deck = Deck(flip: true);
        Assert.DoesNotContain(new OdpWriter().CheckFidelity(deck).Losses, loss => loss.Code == "ODP004");
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter(false, rasterizer).Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        {
            using var reader = archive.GetEntry("content.xml")!.Open();
            var content = System.Xml.Linq.XDocument.Load(reader);
            var draw = PulseOffice.Formats.Packaging.OdfPackage.Namespaces.Draw;
            var images = content.Descendants(draw + "image").ToList();
            Assert.Equal(["image/svg+xml", "image/png"], images.Select(image => (string?)image.Attribute(draw + "mime-type")));
            Assert.Same(images[0].Parent, images[1].Parent);
        }

        buffer.Position = 0;
        var fromOdp = new OdpReader(_themes, _masters).Read(buffer).Value!;
        Assert.True(Vector(fromOdp).FlipHorizontal);
        Assert.Contains("M0 0H10V10Z", Vector(fromOdp).Asset.SvgContent, StringComparison.Ordinal);

        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(fromOdp, pptx).Succeeded);
        pptx.Position = 0;
        Assert.Contains("M0 0H10V10Z", Vector(new PptxReader(_themes, _masters).Read(pptx).Value!).Asset.SvgContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Senza_rasterizzatore_la_riserva_e_comunque_un_PNG()
    {
        var png = new VectorFallback().Png(System.Text.Encoding.UTF8.GetBytes(Svg), new DocumentSize(new Twip(100), new Twip(100)));
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
    }

    [Fact]
    public void In_DOCX_un_SVG_si_scrive_come_svgBlip_con_PNG_di_riserva_e_si_rilegge_come_SVG()
    {
        var rasterizer = new MarkedRasterizer();
        var composer = new PulseOffice.Formats.OpenXml.Wordprocessing.DocxComposer(new PulseOffice.Documents.Styles.DirectFormatting(),
            new PulseOffice.Documents.Styles.DefaultStyleSheetFactory(), new SignatureLineImage(), new VectorFallback(rasterizer));
        var document = new PulseOffice.Documents.TextDocument(new PulseOffice.Documents.Styles.DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PulseOffice.Documents.Styles.PageStyle(PulseOffice.Documents.Styles.DefaultStyleNames.Standard));
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock(document.NextId("p"), "x", PulseOffice.Documents.Styles.DefaultStyleNames.Standard);
        document.Body.Add(paragraph);
        document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("vec", new PulseOffice.Documents.Objects.AsCharacterAnchor(paragraph.Id),
            new PulseOffice.Documents.Objects.EmbeddedContent(new VectorGraphicObject
            {
                Id = "vec",
                Asset = new VectorAsset { Id = "vec", Name = "vec", SourceKind = VectorSourceKind.InlineSvg, SvgContent = Svg },
            }),
            new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(2))));
        paragraph.Marks = [new PulseOffice.Documents.Marks.ObjectMark(0, "vec")];

        using var buffer = new MemoryStream();
        Assert.True(composer.Writer(PulseOffice.Formats.OpenXml.Wordprocessing.DocxFlavour.Document).Write(document, buffer).Succeeded);
        Assert.Equal(1, rasterizer.Calls);
        buffer.Position = 0;
        using (var word = WordprocessingDocument.Open(buffer, false))
            Assert.Single(word.MainDocumentPart!.Document!.Descendants<SVG.SVGBlip>());
        buffer.Position = 0;
        var read = composer.Reader(PulseOffice.Formats.OpenXml.Wordprocessing.DocxFlavour.Document).Read(buffer).Value!;
        var vector = read.Objects.All.Select(item => item.Content).OfType<PulseOffice.Documents.Objects.EmbeddedContent>().Select(content => content.Value).OfType<VectorGraphicObject>().Single();
        Assert.Contains("M0 0H10V10Z", vector.Asset.SvgContent, StringComparison.Ordinal);
    }
}
