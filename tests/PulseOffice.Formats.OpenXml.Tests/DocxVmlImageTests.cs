using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxVmlImageTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream WithVmlPicture(PartTypeInfo type, byte[] data)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "x", DefaultStyleNames.Standard));
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart!;
            var image = main.AddImagePart(type);
            using (var target = image.GetStream(FileMode.Create)) target.Write(data);
            var shape = new DocumentFormat.OpenXml.Vml.Shape(new DocumentFormat.OpenXml.Vml.ImageData { RelationshipId = main.GetIdOfPart(image), Title = string.Empty })
            {
                Id = "_x0000_i1025",
                Style = "width:72pt;height:36pt",
            };
            main.Document!.Body!.Elements<W.Paragraph>().First().Append(new W.Run(new W.Picture(shape)));
        }
        stream.Position = 0;
        return stream;
    }

    private RasterImageObject Image(TextDocument document) =>
        Assert.IsType<RasterImageObject>(Assert.IsType<EmbeddedContent>(Assert.Single(document.Objects.All).Content).Value);

    [Fact]
    public void Le_immagini_VML_di_Word_si_importano()
    {
        using var stream = WithVmlPicture(ImagePartType.Png, Png);
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX102");
        var item = Assert.Single(read.Value!.Objects.All);
        Assert.Equal(Png, Image(read.Value!).Data.ToArray());
        Assert.Equal(PulseOffice.Foundation.Units.Twip.FromPoints(72), item.Size.Width);
    }

    [Fact]
    public void Le_immagini_WMF_restano_WMF_riscrivendo_in_DOCX()
    {
        var wmf = new byte[] { 0xD7, 0xCD, 0xC6, 0x9A, 0, 0, 0, 0 };
        using var stream = WithVmlPicture(ImagePartType.Wmf, wmf);
        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        Assert.Equal("image/x-wmf", Image(document).MediaType);

        using var again = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, again).Succeeded);
        again.Position = 0;
        using var package = WordprocessingDocument.Open(again, false);
        Assert.Contains(package.MainDocumentPart!.ImageParts, part => part.ContentType == "image/x-wmf");
    }

    [Fact]
    public void I_paragrafi_con_cornice_di_Word_diventano_una_cornice()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "dopo", DefaultStyleNames.Standard));
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var body = package.MainDocumentPart!.Document!.Body!;
            W.Paragraph Framed(string text) => new(
                new W.ParagraphProperties(new W.FrameProperties { Width = "2880", Wrap = W.TextWrappingValues.NotBeside, VerticalPosition = W.VerticalAnchorValues.Page, HorizontalPosition = W.HorizontalAnchorValues.Page, X = "1440", Y = "2880" }),
                new W.Run(new W.Text(text)));
            body.InsertAt(Framed("Due"), 0);
            body.InsertAt(Framed("Uno"), 0);
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var frame = Assert.Single(read.Objects.All);
        var box = Assert.IsType<TextBoxContent>(frame.Content);

        Assert.Equal(["Uno", "Due"], box.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.Equal(new PulseOffice.Foundation.Units.Twip(2880), frame.Size.Width);
        Assert.Equal(PulseOffice.Documents.Objects.HorizontalRelation.Page, frame.Placement.Horizontal);
        Assert.Equal(new PulseOffice.Foundation.Units.Twip(2880), frame.Placement.Y);
        Assert.DoesNotContain(read.Body.Blocks.OfType<ParagraphBlock>(), paragraph => paragraph.Text == "Uno");
    }
}
