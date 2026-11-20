using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round336SectionBreakReplaceTests
{
    [Fact]
    public void Replacing_a_section_block_boundary_removes_the_section()
    {
        var fixture = new Fixture();
        var document = new TextDocument();
        var section = new SectionBlock("s", "Section1", new BlockContainer([new ParagraphBlock("in", "dentro")]));
        document.Body.Replace([new ParagraphBlock("before", "prima"), section, new ParagraphBlock("after", "dopo")]);
        var stories = new SearchStories(fixture.Stories, fixture.Tree).From(document, "before");
        var entry = new DocumentSearch().Matches(stories, new SearchQuery("^b")).First();

        new SearchReplacing(fixture.Stories).Replace(fixture.Editor, document, new PulseOffice.Document.Commands.UndoStack(), entry, string.Empty, null);

        Assert.DoesNotContain(document.Body.Blocks, block => block is SectionBlock);
    }
}
