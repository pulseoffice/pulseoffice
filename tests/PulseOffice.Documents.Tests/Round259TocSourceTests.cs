using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round259TocSourceTests
{
    private readonly Fixture _f = new();

    private sealed class NoPages : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => "1";

        public string? NoteCitation(string noteId) => null;

        public string? ListLabel(string paragraphId) => null;

        public int? PageCount => 1;
    }

    [Fact]
    public void The_contents_start_at_the_first_level_and_take_the_source_styles()
    {
        _f.Document.Body.Replace(
        [
            new ParagraphBlock("p0", "", DefaultStyleNames.TextBody),
            new ParagraphBlock("p1", "Top", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock("p2", "Second", DefaultStyleNames.HeadingLevel(2)),
            new ParagraphBlock("p3", "Extra", DefaultStyleNames.Caption),
        ]);
        var definition = new IndexDefinition(IndexKind.Contents) { Levels = 3, FirstLevel = 2, FromMarks = false, SourceStyles = IndexSourceStyles.None.With(DefaultStyleNames.Caption, 1) };

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p0", 0), definition, new NoPages());

        var lines = _f.Tree.Paragraphs(_f.IndexEditing.Indexes(_f.Document).Single().Content).Select(paragraph => paragraph.Text).ToList();
        Assert.Equal(["Second\t1", "Extra\t1"], lines);
    }
}
