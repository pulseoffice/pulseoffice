using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxSmartArtFallbackTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/smartart-rotation2.pptx";

    [Fact]
    public void LoSmartArtSiLeggeDalDisegnoDiRipiego()
    {
        Assert.SkipWhen(!File.Exists(Corpus), "Reference corpus not available.");
        using var stream = File.OpenRead(Corpus);
        var result = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!.Slides[0].Elements, element => element is ObjectElement { Content: ShapeObject } or TextElement);
    }

    private Presentation ReadCorpus()
    {
        using var stream = File.OpenRead(Corpus);
        return new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream).Value!;
    }

    private DocumentFormat.OpenXml.Packaging.PresentationDocument Written(Presentation deck, MemoryStream buffer)
    {
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, false);
    }

    private int Diagrams(DocumentFormat.OpenXml.Packaging.PresentationDocument document) =>
        document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<DocumentFormat.OpenXml.Presentation.GraphicFrame>())
            .Count(frame => frame.Graphic?.GraphicData?.Uri?.Value == "http://schemas.openxmlformats.org/drawingml/2006/diagram");

    [Fact]
    public void Lo_SmartArt_intatto_torna_diagramma_in_PPTX_e_si_dichiara_in_ODP()
    {
        Assert.SkipWhen(!File.Exists(Corpus), "Reference corpus not available.");
        var deck = ReadCorpus();
        Assert.NotEmpty(deck.Slides[0].Diagrams);

        using var buffer = new MemoryStream();
        using (var document = Written(deck, buffer))
        {
            Assert.Equal(1, Diagrams(document));
            Assert.NotEmpty(document.PresentationPart!.SlideParts.First().DiagramDataParts);
        }
        Assert.Contains("ODP025", new PulseOffice.Formats.Odf.OdpWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));

        buffer.Position = 0;
        var again = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(buffer).Value!;
        Assert.NotEmpty(again.Slides[0].Diagrams);
    }

    [Fact]
    public void Lo_SmartArt_modificato_si_scrive_come_forme()
    {
        Assert.SkipWhen(!File.Exists(Corpus), "Reference corpus not available.");
        var deck = ReadCorpus();
        var member = deck.Slides[0].Diagrams[0].Members[0];
        deck.Slides[0].Replace(member with { Bounds = member.Bounds with { Left = new PulseOffice.Foundation.Units.Twip(member.Bounds.Left.Value + 500) } });

        using var buffer = new MemoryStream();
        using var document = Written(deck, buffer);
        Assert.Equal(0, Diagrams(document));
    }
}
