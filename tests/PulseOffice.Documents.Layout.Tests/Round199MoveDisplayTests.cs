using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round199MoveDisplayTests
{
    private sealed class ChosenMoves(MoveDisplay display) : IMoveDisplayOptions
    {
        public MoveDisplay Current { get; } = display;
    }

    private IChangeMarkup Markup(MoveDisplay display) => new ServiceCollection().AddDocumentsModel().AddDocumentsLayout()
        .AddSingleton<IMoveDisplayOptions>(new ChosenMoves(display)).BuildServiceProvider().GetRequiredService<IChangeMarkup>();

    private TextDocument Document()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Changes.Add(new Deletion("from", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("r1", new TextParagraph("gone") { Runs = [new TextRun("gone")] })])) { MoveId = "m1" });
        document.Changes.Add(new Insertion("to", "Ada", DateTimeOffset.UnixEpoch) { MoveId = "m1" });
        document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("stays here") { Runs = [new TextRun("stays here")] })
            {
                Marks = [new ChangeMark(0, "from", MarkEdge.Point), new ChangeMark(6, "to", MarkEdge.Start), new ChangeMark(10, "to", MarkEdge.End)],
            },
        ]);
        return document;
    }

    private MarkedParagraph Marked(MoveDisplay display)
    {
        var document = Document();
        var block = (ParagraphBlock)document.Body.Blocks[0];
        return Markup(display).Mark(document, block, block.Content)!;
    }

    [Fact]
    public void A_chosen_colour_and_marks_replace_the_default_green()
    {
        var marked = Marked(new MoveDisplay(0xFF2563EB, MovedFromMark.Strikethrough, MovedToMark.Bold));

        var inserted = marked.Content.Runs.Last();
        Assert.True(inserted.Bold);
        Assert.False(inserted.DoubleUnderline);
        Assert.Equal((uint?)0xFF2563EBu, inserted.Color);
        var source = Assert.Single(marked.Deletions);
        Assert.False(source.Moved);
        Assert.Equal((uint?)0xFF2563EBu, source.Ink);
    }

    [Fact]
    public void By_author_moves_take_the_author_colour_of_plain_changes()
    {
        var moved = Marked(new MoveDisplay(null, MovedFromMark.DoubleStrikethrough, MovedToMark.Underline)).Content.Runs.Last();

        Assert.True(moved.Underline);
        Assert.NotEqual((uint?)0xFF008A00u, moved.Color);
        Assert.NotNull(moved.Color);
    }
}
