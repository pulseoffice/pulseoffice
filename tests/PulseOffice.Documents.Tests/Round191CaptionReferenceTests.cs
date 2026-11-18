using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round191CaptionReferenceTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void A_caption_reference_names_the_sequence_field_of_the_chosen_category()
    {
        var caption = new TextParagraph(string.Empty).WithRuns(
        [
            new TextRun("Figure "),
            new TextRun("1") { Field = TextFieldKind.Sequence, FieldArgument = "Figure" },
            new TextRun(", Table "),
            new TextRun("2") { Field = TextFieldKind.Sequence, FieldArgument = "Table" },
        ]);
        _f.Document.Body.Replace([new ParagraphBlock("c1", caption)]);
        var targets = new ReferenceTargets(_f.Bookmarks, _f.Outline, new DocumentReadingOrder(_f.Tree, _f.Stories), _f.Stories, new FieldRunRewriter());
        var target = targets.Targets(_f.Document, ReferenceTargetKind.Captions, "Table").Single();

        var name = targets.Prepare(_f.Document, new EditRecorder(_f.Document, "reference"), target);

        var runs = ((ParagraphBlock)_f.Document.Body.Blocks[0]).Content.Runs.Where(run => run.Field == TextFieldKind.Sequence).ToList();
        Assert.StartsWith("RefTable", name);
        Assert.Null(runs[0].FieldDetail?.Format);
        Assert.Equal(name, runs[1].FieldDetail?.Format);
        Assert.Null(targets.Touches(_f.Document, target));
    }
}
