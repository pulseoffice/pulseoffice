using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round167HeadingReferenceTests
{
    private readonly Fixture _f = new();

    private ReferenceTargets Targets() =>
        new(_f.Bookmarks, _f.Outline, new DocumentReadingOrder(_f.Tree, _f.Stories), _f.Stories, new FieldRunRewriter());

    [Fact]
    public void A_heading_bookmark_left_by_an_earlier_session_is_never_given_to_another_heading()
    {
        var reopened = new ParagraphBlock("p1", "First", DefaultStyleNames.HeadingLevel(1))
        {
            Marks = [new BookmarkMark(0, "__RefHeading__p2", MarkEdge.Start), new BookmarkMark(5, "__RefHeading__p2", MarkEdge.End)],
        };
        var second = new ParagraphBlock("p2", "Second", DefaultStyleNames.HeadingLevel(1));
        _f.Document.Body.Replace([reopened, second]);
        var targets = Targets();

        var name = targets.Prepare(_f.Document, new EditRecorder(_f.Document, "reference"), new ReferenceTarget(ReferenceTargetKind.Headings, "p2", "Second"));

        Assert.NotEqual("__RefHeading__p2", name);
        Assert.Equal("p2", _f.Bookmarks.Find(_f.Document, name)!.ParagraphId);
        Assert.Equal("p1", targets.HeadingOf(_f.Document, "__RefHeading__p2"));
        Assert.Equal("__RefHeading__p2", targets.Prepare(_f.Document, new EditRecorder(_f.Document, "reference"), new ReferenceTarget(ReferenceTargetKind.Headings, "p1", "First")));
    }
}
