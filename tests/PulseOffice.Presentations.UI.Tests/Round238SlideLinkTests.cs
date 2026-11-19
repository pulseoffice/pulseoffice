using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round238SlideLinkTests
{
    private static TextElement Text(string id, params TextRun[] runs) => new()
    {
        Id = id,
        Text = new RichText(PlaceholderRole.Free, [new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs }]),
        Bounds = new DocumentRect(new Twip(100), new Twip(200), new Twip(300), new Twip(400)),
    };

    [Fact]
    public void Shape_hyperlinks_and_wholly_linked_text_become_link_areas()
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.Add(Text("shape", new TextRun("Go")));
        slide.Add(Text("text", new TextRun("Site") { Hyperlink = "https://example.org" }));
        slide.Add(Text("mixed", new TextRun("a") { Hyperlink = "https://a.example" }, new TextRun("b") { Hyperlink = "https://b.example" }));
        slide.AddInteraction(new InteractiveAction { TargetId = "shape", Kind = InteractionKind.Hyperlink, Reference = "https://shape.example" });

        var areas = new SlideLinkAreas().For(slide);

        Assert.Equal(["https://shape.example", "https://example.org"], areas.Select(area => area.Target));
        Assert.Equal(new Twip(100), areas[0].Bounds.Left);
    }
}
