using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round306TableCellTextRebaseTests
{
    private static TableCellText Rich() => new(0, 0,
    [
        new TableCellParagraph([new TableTextRun("ab") { Bold = true }, new TableTextRun("cd")]),
        new TableCellParagraph([new TableTextRun("ef") { Italic = true }]),
    ]);

    [Fact]
    public void Text_typed_inside_a_run_takes_its_format_and_the_other_runs_stay()
    {
        var rebased = Rich().Rebased("abXcd\nef")!;

        Assert.Equal("abXcd\nef", rebased.PlainText);
        var first = rebased.Paragraphs[0].Runs;
        Assert.Equal("abX", first[0].Text);
        Assert.True(first[0].Bold);
        Assert.Equal("cd", first[1].Text);
        Assert.True(rebased.Paragraphs[1].Runs[0].Italic);
    }

    [Fact]
    public void Removing_the_formatted_text_leaves_no_rich_text()
    {
        Assert.Null(new TableCellText(0, 0, [new TableCellParagraph([new TableTextRun("ab") { Bold = true }, new TableTextRun("cd")])]).Rebased("cd"));
    }

    [Fact]
    public void Stacked_text_gives_one_paragraph_per_character_with_its_format()
    {
        var stacked = Rich().Stacked();

        Assert.Equal("a\nb\nc\nd\ne\nf", stacked.PlainText);
        Assert.True(stacked.Paragraphs[1].Runs[0].Bold);
        Assert.Null(stacked.Paragraphs[2].Runs[0].Bold);
    }
}
