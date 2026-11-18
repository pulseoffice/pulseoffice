using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round157TrackChangesTests
{
    private sealed class FixedAuthor : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
    }

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new FixedAuthor()).BuildServiceProvider();
    private readonly UndoStack _history = new();

    private IDocumentEditor Editor => _services.GetRequiredService<IDocumentEditor>();

    private TextDocument Through(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    [Fact]
    public void A_format_change_that_merges_runs_keeps_every_previous_run_in_docx()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        var first = (ParagraphBlock)document.Body[0];
        Editor.InsertText(document, _history, new DocumentPosition(first.Id, 0), "abcd");
        Editor.SetCharacter(document, _history, DocumentRange.In(first.Id, 2, 4), new CharacterProperties { Italic = true });
        document.Changes.IsRecording = true;
        Editor.SetCharacter(document, _history, DocumentRange.In(first.Id, 0, 4), new CharacterProperties { Italic = false });
        Assert.Single(((ParagraphBlock)document.Body[0]).Content.Runs);

        var read = Through(document);
        _services.GetRequiredService<IRevisionResolver>().ResolveAll(read, new UndoStack(), RevisionVerdict.Reject);

        var runs = ((ParagraphBlock)read.Body[0]).Content.Runs;
        Assert.Equal(["ab", "cd"], runs.Select(run => run.Text));
        Assert.True(runs[1].Italic);
    }
}
