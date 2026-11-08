using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxFourteenthAuditTests
{
    private const string GroupedSmartArt = "/Volumes/Dati/develop/libreoffice-ref/sd/qa/unit/data/pptx/tdf148921.pptx";

    [Fact]
    public void Un_gruppo_che_contiene_uno_SmartArt_resta_un_gruppo()
    {
        Assert.SkipWhen(!File.Exists(GroupedSmartArt), "Reference corpus not available.");
        Presentation deck;
        using (var stream = File.OpenRead(GroupedSmartArt))
            deck = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream).Value!;
        Assert.Contains(deck.Slides[0].Groups, group => group.Members.Count > 1);

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, false);
        var group = Assert.Single(document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Elements<P.GroupShape>());
        Assert.Single(group.Elements<P.GraphicFrame>());
        Assert.Single(group.Elements<P.Shape>());
    }
}
