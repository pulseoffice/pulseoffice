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
        var objects = new AnchoredObjectCollection();
        for (var index = 0; index < 5_000; index++) objects.Add(Box("o" + index, "p" + index * 4));
        var clock = Stopwatch.StartNew();
        var found = 0;
        for (var index = 0; index < 20_000; index++)
        {
            found += objects.AnchoredTo("p" + index).Count();
            if (objects.Find("o" + index % 5_000) is null) found = -1_000_000;
        }
        clock.Stop();
        Assert.Equal(5_000, found);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"looked up in {clock.Elapsed}");
    }

    [Fact]
    public void FindingEveryNoteOfALongBookCostsLinearTime()
    {
        var notes = new PulseOffice.Documents.Annotations.NoteCollection();
        for (var index = 0; index < 30_000; index++) notes.Add(new PulseOffice.Documents.Annotations.Note("fn" + index, PulseOffice.Documents.Annotations.NoteKind.Footnote, new BlockContainer()));
        var clock = Stopwatch.StartNew();
        var found = Enumerable.Range(0, 30_000).Count(index => notes.Find("fn" + index) is not null);
        clock.Stop();
        Assert.Equal(30_000, found);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"found in {clock.Elapsed}");
        notes.Remove("fn5");
        Assert.Null(notes.Find("fn5"));
    }

    [Fact]
    public void FindingEveryCommentOfAReviewedDocumentCostsLinearTime()
    {
        var comments = new PulseOffice.Documents.Annotations.CommentCollection();
        for (var index = 0; index < 30_000; index++) comments.Add(new PulseOffice.Documents.Annotations.Comment("c" + index, "A", DateTimeOffset.MinValue, new BlockContainer()));
        var clock = Stopwatch.StartNew();
        var found = Enumerable.Range(0, 30_000).Count(index => comments.Find("c" + index) is not null);
        clock.Stop();
        Assert.Equal(30_000, found);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"found in {clock.Elapsed}");
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
