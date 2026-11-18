using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round162TrackChangesTests
{
    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly IDocumentComparison _comparison;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();
    private readonly RevisionSignature _carla = new("Carla", new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

    public Round162TrackChangesTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _comparison = services.GetRequiredService<IDocumentComparison>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
    }

    private ParagraphBlock Paragraph(int index) => (ParagraphBlock)_document.Body[index];

    [Fact]
    public void Merged_words_keep_the_formatting_of_the_other_document()
    {
        _editor.InsertText(_document, _history, new DocumentPosition(Paragraph(0).Id, 0), "uno tre");
        var other = _document.Clone();
        var copy = (ParagraphBlock)other.Body[0];
        copy.State = copy.State with { Content = copy.Content.WithRuns([new TextRun("uno "), new TextRun("due") { Bold = true }, new TextRun(" tre")]) };

        _comparison.Merge(_document, other, _history, _carla);

        Assert.Equal("uno due tre", Paragraph(0).Text);
        Assert.Contains(Paragraph(0).Content.Runs, run => run.Text == "due" && run.Bold);
        Assert.DoesNotContain(Paragraph(0).Content.Runs, run => run.Text.Contains("uno", StringComparison.Ordinal) && run.Bold);
    }

    [Fact]
    public void Merged_paragraphs_keep_their_style_and_runs()
    {
        _editor.InsertText(_document, _history, new DocumentPosition(Paragraph(0).Id, 0), "Primo");
        var other = _document.Clone();
        other.Body.Add(new ParagraphBlock("added", new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Title }.WithRuns([new TextRun("Nuovo") { Italic = true }])));

        _comparison.Merge(_document, other, _history, _carla);

        Assert.Equal(2, _document.Body.Count);
        Assert.Equal("Nuovo", Paragraph(1).Text);
        Assert.Equal(DefaultStyleNames.Title, Paragraph(1).Content.StyleName);
        Assert.All(Paragraph(1).Content.Runs, run => Assert.True(run.Italic));
        Assert.Equal("Primo", Paragraph(0).Text);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);
        Assert.Single(_document.Body.Blocks);
        Assert.Equal("Primo", Paragraph(0).Text);
    }
}
