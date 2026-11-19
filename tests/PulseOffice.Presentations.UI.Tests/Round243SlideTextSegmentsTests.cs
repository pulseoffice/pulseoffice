using PulseOffice.Document.Embedding;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round243SlideTextSegmentsTests
{
    [Fact]
    public void A_text_box_gives_one_element_per_paragraph_and_bullets_become_a_list()
    {
        var text = new TextElement
        {
            Id = "body",
            Text = new RichText(PlaceholderRole.Body, [
                new TextParagraph("Intro"),
                new TextParagraph("Uno") { Bullet = true },
                new TextParagraph("Due") { Bullet = true },
            ]),
        };
        var container = new PrintTag("box", PrintRole.Section);

        var segments = new SlideTextSegments().For(container, text);

        Assert.NotNull(segments);
        Assert.Equal(new[] { PrintRole.Paragraph, PrintRole.ListBody, PrintRole.ListBody }, segments.Segments.Select(segment => segment.Tag.Role).ToArray());
        Assert.Equal(PrintRole.ListLabel, segments.Segments[1].Label?.Role);
        Assert.Same(segments.Segments[1].Tag.Parent?.Parent, segments.Segments[2].Tag.Parent?.Parent);
        Assert.Equal(PrintRole.List, segments.Segments[2].Tag.Parent?.Parent?.Role);
    }

    [Fact]
    public void A_table_is_tagged_as_a_table_with_header_cells_and_spans()
    {
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = "c", Sql = "s" },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("Zona", DataValueKind.Text), new DataColumn("Totale", DataValueKind.Number)], Rows = [["Nord", null]] },
            Merges = [new TableMerge(0, 0, 1, 2)],
        };
        var slide = new Slide("s1", SlideLayout.Blank);
        var element = new ObjectElement { Id = "t", Content = table };
        slide.Add(element);

        var tag = new SlideStructureTags().For(slide, element);
        var segments = new SlideTextSegments().For(tag!, element);

        Assert.Equal(PrintRole.Table, tag?.Role);
        Assert.NotNull(segments);
        Assert.Equal(new[] { PrintRole.TableHeader, PrintRole.TableHeader, PrintRole.TableCell }, segments.Segments.Select(segment => segment.Tag.Role).ToArray());
        Assert.Equal(2, segments.Segments[2].Tag.ColumnSpan);
    }
}
