using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round239SlideStructureTests
{
    private static TextElement Text(string id, PlaceholderRole role, string text, int z) =>
        new() { Id = id, ZOrder = z, Text = new RichText(role, [new TextParagraph(text)]) };

    [Fact]
    public void The_title_reads_first_then_the_other_elements_in_stacking_order_and_empty_text_is_skipped()
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.Add(Text("body", PlaceholderRole.Body, "Punti", 1));
        slide.Add(Text("empty", PlaceholderRole.Free, "  ", 2));
        slide.Add(Text("title", PlaceholderRole.Title, "Bilancio", 3));
        var tags = new SlideStructureTags();

        var order = tags.ReadingOrder(slide);

        Assert.Equal(new[] { PrintRole.Title, PrintRole.Section }, order.Select(tag => tag.Role).ToArray());
        Assert.All(order, tag => Assert.Equal(PrintRole.Slide, tag.Parent?.Role));
        Assert.Null(tags.For(slide, Text("master", PlaceholderRole.Free, "Logo", 0)));
        Assert.Null(tags.For(slide, null));
    }
}
