using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxForeignQuirkTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private MemoryStream Written(Action<Presentation> configure, Action<PresentationDocument> edit)
    {
        var theme = _themes.Find("Light")!;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme), Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        configure(deck);
        var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        using (var document = PresentationDocument.Open(buffer, true)) edit(document);
        buffer.Position = 0;
        return buffer;
    }

    [Fact]
    public void PromptsInTheNotesMasterAreNotReadAsText()
    {
        using var buffer = Written(deck => deck.NotesMaster = deck.NotesMaster with { Header = "Riunione" }, document =>
        {
            var header = document.PresentationPart!.NotesMasterPart!.NotesMaster!.Descendants<P.Shape>()
                .First(shape => shape.Descendants<P.PlaceholderShape>().Any(ph => ph.Type?.Value == P.PlaceholderValues.Header));
            header.Descendants<D.Text>().First().Text = "<intestazione>";
        });

        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;
        Assert.Equal(string.Empty, back.NotesMaster.Header);
    }

    [Fact]
    public void AVisibleNameWithoutOurExtensionIsKept()
    {
        using var buffer = Written(deck => deck.Slides[0].Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Ellipse },
        }), document =>
        {
            var properties = document.PresentationPart!.SlideParts.First().Slide!.Descendants<P.NonVisualDrawingProperties>().First(p => p.Name?.Value == "DeckShape");
            properties.Name = "Logo";
        });

        var shape = new PptxReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements.OfType<ObjectElement>().Single();
        Assert.Equal("Logo", shape.Content.Name);
    }
}
