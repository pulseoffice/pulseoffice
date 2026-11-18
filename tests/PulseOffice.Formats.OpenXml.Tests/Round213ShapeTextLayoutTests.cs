using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round213ShapeTextLayoutTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private MemoryStream Pptx()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "a", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(1000)), Content = new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle, Text = "x" } });
        deck.AddExisting(slide);
        var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return buffer;
    }

    [Fact]
    public void Vertical_shape_text_read_from_PPTX_is_declared()
    {
        using var buffer = Pptx();
        Assert.DoesNotContain(new PptxReader(_themes, _masters).Read(buffer).Diagnostics, diagnostic => diagnostic.Code == "PPTX226");
        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, true))
            document.PresentationPart!.SlideParts.First().Slide!.Descendants<D.BodyProperties>().First().Vertical = new D.TextVerticalValues("eaVert");
        buffer.Position = 0;
        Assert.DoesNotContain(new PptxReader(_themes, _masters).Read(buffer).Diagnostics, diagnostic => diagnostic.Code == "PPTX226");
    }

    [Fact]
    public void Vertical_shape_text_read_from_ODP_is_declared()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
            Add("mimetype", "application/vnd.oasis.opendocument.presentation");
            Add("content.xml", "<?xml version=\"1.0\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
                "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"><office:automatic-styles>" +
                "<style:style style:name=\"gr1\" style:family=\"graphic\"><style:graphic-properties style:writing-mode=\"tb-lr\"/></style:style></office:automatic-styles>" +
                "<office:body><office:presentation><draw:page draw:name=\"p\"><draw:rect draw:style-name=\"gr1\" svg:width=\"4cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"/>" +
                "</draw:page></office:presentation></office:body></office:document-content>");
        }
        buffer.Position = 0;
        Assert.DoesNotContain(new OdpReader(_themes, _masters).Read(buffer).Diagnostics, diagnostic => diagnostic.Code == "ODP217");
    }
}
