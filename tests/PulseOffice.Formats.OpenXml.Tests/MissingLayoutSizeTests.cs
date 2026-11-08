using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class MissingLayoutSizeTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck()
    {
        var theme = _themes.Find("Meridian")!;
        var deck = new Presentation { Size = SlideSize.Standard, Theme = theme, Master = _masters.CreateStandard(theme, SlideSize.Standard) };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        return deck;
    }

    private void AssertInside(Presentation deck, SlideLayout kind) =>
        Assert.All(deck.Master.Template(kind).Placeholders, placeholder =>
            Assert.True(placeholder.Bounds.Left.Value + placeholder.Bounds.Width.Value <= SlideSize.Standard.Width.Value + 1));

    [Fact]
    public void UnLayoutMancanteInPptxNasceNellaMisuraDellaPresentazione()
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), stream).Succeeded);
        using (var package = PresentationDocument.Open(stream, true))
        {
            var master = package.PresentationPart!.SlideMasterParts.First();
            foreach (var layout in master.SlideLayoutParts.Where(part => part.SlideLayout?.Type?.Value == P.SlideLayoutValues.SectionHeader).ToList())
            {
                var id = master.GetIdOfPart(layout);
                master.SlideMaster!.SlideLayoutIdList!.Elements<P.SlideLayoutId>().First(entry => entry.RelationshipId == id).Remove();
                master.DeletePart(layout);
            }
        }

        stream.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(stream).Value!;
        AssertInside(back, SlideLayout.SectionHeader);
    }

    [Fact]
    public void UnLayoutMancanteInOdpNasceNellaMisuraDellaPresentazione()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), stream).Succeeded);
        stream.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(stream).Value!;
        AssertInside(back, SlideLayout.SectionHeader);
        AssertInside(back, SlideLayout.Comparison);
    }
}
