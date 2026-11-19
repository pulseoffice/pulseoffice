using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round263TocBookmarkReuseTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void A_heading_with_a_word_toc_bookmark_keeps_it_as_its_anchor()
    {
        var heading = new ParagraphBlock("h1", "Intro", DefaultStyleNames.HeadingLevel(1));
        heading.State = heading.State with { Marks = [new BookmarkMark(0, "_Toc12345", MarkEdge.Start), new BookmarkMark(5, "_Toc12345", MarkEdge.End)] };
        _f.Document.Body.Replace([heading]);
        var targets = new ReferenceTargets(_f.Bookmarks, _f.Outline, new DocumentReadingOrder(_f.Tree, _f.Stories), _f.Stories, new FieldRunRewriter());

        var name = targets.Prepare(_f.Document, new EditRecorder(_f.Document, "toc"), new ReferenceTarget(ReferenceTargetKind.Headings, "h1", "Intro"));

        Assert.Equal("_Toc12345", name);
        Assert.Equal(2, heading.Marks.Count);
        Assert.Equal("h1", targets.HeadingOf(_f.Document, "_Toc12345"));
    }
}

public sealed class Round263TocSourceStyleAssignTests
{
    [Fact]
    public void Assigning_levels_adds_changes_and_removes_with_zero()
    {
        var start = Indexes.IndexSourceStyles.None.With("Caption", 2).With("Quote", 1);

        var assigned = start.Assign([new("Caption", 0), new("Quote", 3), new("Title", 1)]);

        Assert.Null(assigned.LevelOf("Caption"));
        Assert.Equal(3, assigned.LevelOf("Quote"));
        Assert.Equal(1, assigned.LevelOf("Title"));
    }
}
