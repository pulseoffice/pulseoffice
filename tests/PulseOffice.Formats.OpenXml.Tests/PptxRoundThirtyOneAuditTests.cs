using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxRoundThirtyOneAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Theory]
    [InlineData("ppaction://program", true)]
    [InlineData("ppaction://ole?verb=0", true)]
    [InlineData("ppaction://hlinkshowjump?jump=nextslide", false)]
    public void Run_program_and_object_verb_actions_are_declared_when_read(string action, bool declared)
    {
        var theme = _themes.Default;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        using (var document = PresentationDocument.Open(stream, true))
        {
            var drawing = new P.NonVisualDrawingProperties { Id = 70, Name = "Button" };
            drawing.Append(new D.HyperlinkOnClick { Id = string.Empty, Action = action });
            document.PresentationPart!.SlideParts.First().Slide!.CommonSlideData!.ShapeTree!.Append(new P.Shape(
                new P.NonVisualShapeProperties(drawing, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(new D.Transform2D(new D.Offset { X = 0, Y = 0 }, new D.Extents { Cx = 914400, Cy = 914400 }),
                    new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle }),
                new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph())));
        }

        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream);

        Assert.True(read.Succeeded);
        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "PPTX185"));
    }
}
