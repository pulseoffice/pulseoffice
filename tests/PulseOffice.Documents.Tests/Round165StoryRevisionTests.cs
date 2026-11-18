using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round165StoryRevisionTests
{
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public Round165StoryRevisionTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
    }

    [Fact]
    public void Rejecting_a_format_change_over_two_paragraphs_restores_the_previous_formatting_of_both()
    {
        _document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("xab").WithRuns([new TextRun("x"), new TextRun("ab") { Bold = true }]))
            {
                Marks = [new ChangeMark(1, "f1", MarkEdge.Start)],
            },
            new ParagraphBlock("p2", new TextParagraph("cd").WithRuns([new TextRun("cd") { Bold = true }]))
            {
                Marks = [new ChangeMark(1, "f1", MarkEdge.End)],
            },
        ]);
        _document.Changes.Add(new FormatChange("f1", "Ada", DateTimeOffset.UnixEpoch, [new TextRun("a") { Italic = true }, new TextRun("bc")]));

        Assert.Equal(1, _resolver.Resolve(_document, _history, ["f1"], RevisionVerdict.Reject));

        var first = ((ParagraphBlock)_document.Body[0]).Content.EffectiveRuns();
        var second = ((ParagraphBlock)_document.Body[1]).Content.EffectiveRuns();
        Assert.Equal("xab", string.Concat(first.Select(run => run.Text)));
        Assert.DoesNotContain(first, run => run.Bold);
        Assert.Contains(first, run => run.Text == "a" && run.Italic);
        Assert.Contains(second, run => run.Text.StartsWith('c') && !run.Bold);
        Assert.Contains(second, run => run.Text.EndsWith('d') && run.Bold);
        Assert.Empty(_document.Changes.All);
    }
}
