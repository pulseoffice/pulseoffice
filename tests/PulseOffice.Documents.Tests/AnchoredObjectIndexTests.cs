using System.Diagnostics;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class AnchoredObjectIndexTests
{
    private static AnchoredObject Box(string id, string paragraphId) =>
        new(id, new ParagraphAnchor(paragraphId), new TextBoxContent(new BlockContainer()), new DocumentSize(new Twip(1440), new Twip(1440)));

    [Fact]
    public void ManyParagraphsAskingForTheirObjectsCostLinearTime()
    {
        var found = 0;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            var objects = new AnchoredObjectCollection();
            for (var index = 0; index < size; index++) objects.Add(Box("o" + index, "p" + index * 4));
            return () =>
            {
                found = 0;
                for (var index = 0; index < size * 4; index++)
                {
                    found += objects.AnchoredTo("p" + index).Count();
                    if (objects.Find("o" + index % size) is null) found = -1_000_000;
                }
            };
        }, 5_000);
        Assert.Equal(5_000, found);
    }

    [Fact]
    public void FindingEveryNoteOfALongBookCostsLinearTime()
    {
        var notes = new PulseOffice.Documents.Annotations.NoteCollection();
        var found = 0;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            notes = new PulseOffice.Documents.Annotations.NoteCollection();
            for (var index = 0; index < size; index++) notes.Add(new PulseOffice.Documents.Annotations.Note("fn" + index, PulseOffice.Documents.Annotations.NoteKind.Footnote, new BlockContainer()));
            var current = notes;
            return () => found = Enumerable.Range(0, size).Count(index => current.Find("fn" + index) is not null);
        }, 30_000);
        Assert.Equal(30_000, found);
        notes.Remove("fn5");
        Assert.Null(notes.Find("fn5"));
    }

    [Fact]
    public void FindingEveryCommentOfAReviewedDocumentCostsLinearTime()
    {
        var comments = new PulseOffice.Documents.Annotations.CommentCollection();
        var found = 0;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            comments = new PulseOffice.Documents.Annotations.CommentCollection();
            for (var index = 0; index < size; index++) comments.Add(new PulseOffice.Documents.Annotations.Comment("c" + index, "A", DateTimeOffset.MinValue, new BlockContainer()));
            var current = comments;
            return () => found = Enumerable.Range(0, size).Count(index => current.Find("c" + index) is not null);
        }, 30_000);
        Assert.Equal(30_000, found);
        comments.Remove("c7");
        Assert.Null(comments.Find("c7"));
    }

    [Fact]
    public void MovingAnObjectToAnotherParagraphIsSeenAtOnce()
    {
        var objects = new AnchoredObjectCollection();
        var box = Box("o1", "p1");
        objects.Add(box);
        Assert.Single(objects.AnchoredTo("p1"));
        box.Anchor = new CharacterAnchor("p2");
        Assert.Empty(objects.AnchoredTo("p1"));
        Assert.Same(box, Assert.Single(objects.AnchoredTo("p2")));
        objects.Remove("o1");
        Assert.Empty(objects.AnchoredTo("p2"));
        Assert.Null(objects.Find("o1"));
        box.Anchor = new ParagraphAnchor("p3");
        Assert.Empty(objects.AnchoredTo("p3"));
    }
}
