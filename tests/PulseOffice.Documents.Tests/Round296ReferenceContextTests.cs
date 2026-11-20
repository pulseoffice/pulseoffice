using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round296ReferenceContextTests
{
    private sealed class Labels(Dictionary<string, string> labels) : IFieldLayoutFacts
    {
        public string? PageOf(string paragraphId) => null;

        public string? NoteCitation(string noteId) => null;

        public string? ListLabel(string paragraphId) => labels.GetValueOrDefault(paragraphId);

        public int? PageCount => null;
    }

    private static ParagraphBlock Item(string id, int level) =>
        new(id, new TextParagraph("x") { Runs = [new TextRun("x")], ListStyleName = "L", BulletLevel = level });

    [Theory]
    [InlineData("i)", "2.b.i")]
    [InlineData("2.2.1", "2.2.1")]
    public void A_full_context_number_is_composed_from_the_parents(string targetLabel, string expected)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var target = Item("t", 2);
        target.State = new ParagraphState(target.Content, [new BookmarkMark(0, "mark", MarkEdge.Start), new BookmarkMark(1, "mark", MarkEdge.End)]);
        document.Body.Replace([Item("a", 0), Item("b", 0), Item("c", 1), Item("d", 1), target]);
        var facts = new Labels(new() { ["a"] = "1.", ["b"] = "2.", ["c"] = "a)", ["d"] = "b)", ["t"] = targetLabel });
        var stories = new StoryCatalog(new BlockTree());
        var source = new ReferenceFieldSource(new BookmarkCatalog(stories), stories, new DocumentOutline(new BlockTree(), new StyleResolver()), new InvariantFieldWords());
        var run = new TextRun("?") { Field = TextFieldKind.Bookmark, FieldArgument = "mark", FieldDetail = new FieldDetail { Format = ReferenceFormats.NumberFullContext } };

        var value = source.Evaluate(run, new FieldContext(document, new FieldUpdate(facts, DateTime.MinValue), new Dictionary<string, int>()));

        Assert.Equal(expected, value.Text);
    }
}
