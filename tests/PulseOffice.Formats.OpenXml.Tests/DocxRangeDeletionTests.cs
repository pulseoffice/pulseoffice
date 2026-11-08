using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxRangeDeletionTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument ThroughDocx(TextDocument document, out IReadOnlyList<string> losses)
    {
        using var stream = new MemoryStream();
        var written = _composer.Writer(DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded);
        losses = [.. written.Diagnostics.Select(diagnostic => diagnostic.Code)];
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    [Fact]
    public void UnaCancellazioneAIntervalloComeLaScrivonoLeAltreSuiteArrivaInDocxComeTestoCancellato()
    {
        var document = new TextDocument();
        var deletion = new Deletion("change1", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer());
        document.Changes.Add(deletion);
        document.Body.Add(new ParagraphBlock("p1", "foobarbaz")
        {
            Marks = [new ChangeMark(3, deletion.Id, MarkEdge.Start), new ChangeMark(6, deletion.Id, MarkEdge.End)],
        });

        var copy = ThroughDocx(document, out var losses);

        Assert.DoesNotContain("DOCX114", losses);
        var removed = Assert.Single(copy.Changes.All.OfType<Deletion>());
        Assert.Equal("bar", string.Concat(new BlockTree().Paragraphs(removed.Removed).Select(paragraph => paragraph.Text)));
        Assert.Equal("foobaz", Assert.IsType<ParagraphBlock>(Assert.Single(copy.Body.Blocks)).Text);
    }

    [Fact]
    public void UnaCancellazioneCheAttraversaUnaFineParagrafoDichiaraLaPerditaInDocx()
    {
        var document = new TextDocument();
        var deletion = new Deletion("change1", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer());
        document.Changes.Add(deletion);
        document.Body.Add(new ParagraphBlock("p1", "foo") { Marks = [new ChangeMark(1, deletion.Id, MarkEdge.Start)] });
        document.Body.Add(new ParagraphBlock("p2", "bar") { Marks = [new ChangeMark(1, deletion.Id, MarkEdge.End)] });

        ThroughDocx(document, out var losses);

        Assert.Contains("DOCX114", losses);
    }
}
