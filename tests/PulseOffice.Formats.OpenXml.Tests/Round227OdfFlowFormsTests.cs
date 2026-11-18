using System.IO.Compression;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round227OdfFlowFormsTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        var style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Layout = new TextLayout { Flow = TextFlow.Vertical270 } } };
        slide.Add(new ObjectElement { Id = "a", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(1000)), Content = new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle, Text = "x", Style = style } });
        deck.AddExisting(slide);
        return deck;
    }

    [Fact]
    public void A_270_degree_flow_is_written_as_the_bottom_to_top_writing_mode_and_read_back()
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);

        buffer.Position = 0;
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
            var content = reader.ReadToEnd();
            Assert.Contains("writing-mode=\"bt-lr\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("vertical-270", content, StringComparison.Ordinal);
        }

        buffer.Position = 0;
        var shape = new OdpReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements
            .OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();
        Assert.Equal(TextFlow.Vertical270, shape.Style.Text.Layout.Flow);
    }
}
