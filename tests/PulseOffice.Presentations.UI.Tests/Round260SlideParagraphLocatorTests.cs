using Avalonia;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round260SlideParagraphLocatorTests
{
    private static readonly TextElement Repeated = new()
    {
        Id = "body",
        Text = new RichText(PlaceholderRole.Body, [new TextParagraph("Same"), new TextParagraph("Same") { Bullet = true }]),
    };

    private static readonly SlideElementPlacement Placed = new(0, 0, 200, 100, 0.05, 0)
    {
        Lines = [new RichTextLineBox(0, null, new Rect(10, 10, 100, 20)), new RichTextLineBox(1, new Rect(0, 40, 8, 20), new Rect(10, 40, 100, 20))],
    };

    [Fact]
    public void Paragraphs_with_the_same_text_are_tagged_by_their_line_position()
    {
        var segments = new SlideTextSegments().For(new PrintTag("box", PrintRole.Section), Repeated, Placed)!;
        var locator = segments.Locator!;

        Assert.Equal(PrintRole.Paragraph, locator.At(50, 20)?.Role);
        Assert.Equal(PrintRole.ListBody, locator.At(50, 50)?.Role);
        Assert.Equal(PrintRole.ListLabel, locator.At(4, 50)?.Role);
        Assert.Null(locator.At(50, 90));
    }

    [Fact]
    public void Without_line_boxes_paragraphs_fall_back_to_text_matching()
    {
        var segments = new SlideTextSegments().For(new PrintTag("box", PrintRole.Section), Repeated, Placed with { Lines = [] })!;

        Assert.Null(segments.Locator);
    }
}
