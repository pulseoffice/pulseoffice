using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckFreeTextBoxTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void UnaCasellaDiTestoLiberaRestaCasellaENonDiventaSegnaposto()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new TextElement
        {
            Id = "free1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(3), Twip.FromCentimeters(8), Twip.FromCentimeters(2)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("libera")]),
        });
        deck.AddExisting(slide);

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var package = PresentationDocument.Open(buffer, false))
        {
            var shape = package.PresentationPart!.SlideParts.Single().Slide!.Descendants<P.Shape>().Single();
            Assert.Null(shape.NonVisualShapeProperties!.ApplicationNonVisualDrawingProperties!.GetFirstChild<P.PlaceholderShape>());
            Assert.True(shape.NonVisualShapeProperties!.NonVisualShapeDrawingProperties!.TextBox!.Value);
        }

        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Require();
        var text = back.Slides.Single().Elements.OfType<TextElement>().Single();
        Assert.Equal(PlaceholderRole.Free, text.Text.Role);
        Assert.Equal("libera", text.Text.Paragraphs.Single().Text);
    }
}
