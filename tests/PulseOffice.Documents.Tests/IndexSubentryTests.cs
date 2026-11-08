using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class IndexSubentryTests
{
    private readonly Fixture _f = new();

    private sealed class Pages : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => paragraphId[1..];

        public string? NoteCitation(string noteId) => null;

        public string? ListLabel(string paragraphId) => null;

        public int? PageCount => 9;
    }

    [Fact]
    public void LeSottovociSiAnnidanoSottoLaVocePrincipale()
    {
        _f.Document.Body.Replace(
        [
            new ParagraphBlock("p0", string.Empty, DefaultStyleNames.TextBody),
            new ParagraphBlock("p1", "a", DefaultStyleNames.TextBody) { Marks = [new IndexEntryMark(0, "Mela", MarkEdge.Point) { Key1 = "Frutta" }] },
            new ParagraphBlock("p2", "b", DefaultStyleNames.TextBody) { Marks = [new IndexEntryMark(0, "Banana", MarkEdge.Point) { Key1 = "Frutta" }] },
            new ParagraphBlock("p3", "c", DefaultStyleNames.TextBody) { Marks = [new IndexEntryMark(0, "Cane", MarkEdge.Point)] },
        ]);

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p0", 0), new IndexDefinition(IndexKind.Alphabetical) { Hyperlinks = false }, new Pages());

        var index = _f.IndexEditing.Indexes(_f.Document).Single();
        var lines = _f.Tree.Paragraphs(index.Content).Select(paragraph => (paragraph.Text, paragraph.StyleName)).ToList();
        Assert.Equal(
        [
            ("Cane, 3", DefaultStyleNames.IndexLevel(1)),
            ("Frutta", DefaultStyleNames.IndexLevel(1)),
            ("Banana, 2", DefaultStyleNames.IndexLevel(2)),
            ("Mela, 1", DefaultStyleNames.IndexLevel(2)),
        ], lines);
    }
}
