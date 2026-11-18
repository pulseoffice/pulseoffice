using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round196MoveMarkupTests
{
    private readonly IChangeMarkup _markup = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IChangeMarkup>();

    private static TextDocument Document(string? moveId)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Changes.Add(new Deletion("from", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("r1", new TextParagraph("gone") { Runs = [new TextRun("gone")] })])) { MoveId = moveId });
        document.Changes.Add(new Insertion("to", "Ada", DateTimeOffset.UnixEpoch) { MoveId = moveId });
        document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("stays here") { Runs = [new TextRun("stays here")] })
            {
                Marks = [new ChangeMark(0, "from", MarkEdge.Point), new ChangeMark(6, "to", MarkEdge.Start), new ChangeMark(10, "to", MarkEdge.End)],
            },
        ]);
        return document;
    }

    private MarkedParagraph Marked(TextDocument document)
    {
        var block = (ParagraphBlock)document.Body.Blocks[0];
        return _markup.Mark(document, block, block.Content)!;
    }

    [Fact]
    public void Moved_text_is_double_underlined_and_its_source_double_struck()
    {
        var marked = Marked(Document("m1"));

        var inserted = marked.Content.Runs.Last();
        Assert.True(inserted.DoubleUnderline);
        Assert.False(inserted.Underline);
        var source = Assert.Single(marked.Deletions);
        Assert.True(source.Moved);
        Assert.Equal(inserted.Color, source.Ink);
    }

    [Fact]
    public void Plain_changes_keep_the_single_lines()
    {
        var marked = Marked(Document(null));

        Assert.True(marked.Content.Runs.Last().Underline);
        Assert.False(marked.Content.Runs.Last().DoubleUnderline);
        Assert.False(Assert.Single(marked.Deletions).Moved);
    }
}
