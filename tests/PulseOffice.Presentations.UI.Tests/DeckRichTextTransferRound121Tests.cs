using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRichTextTransferRound121Tests
{
    private sealed class FixedInterchange(TextDocument document) : ITextDocumentInterchange
    {
        public TextDocument? Read(string formatId, ReadOnlyMemory<byte> data) => formatId == "html" ? document : null;

        public byte[]? Write(string formatId, TextDocument document) => null;
    }

    [Fact]
    public void Pasted_html_keeps_the_text_inside_sections_and_tables()
    {
        var document = new TextDocument();
        var section = new BlockContainer();
        section.Add(new ParagraphBlock("s1", new TextParagraph("intro").WithRuns([new TextRun("intro") { Bold = true }])));
        document.Body.Add(new SectionBlock("sec", "Section1", section));
        var cell = new BlockContainer();
        cell.Add(new ParagraphBlock("c1", new TextParagraph("cell")));
        document.Body.Add(new TableBlock("t", [new TableRow([new TableCell(cell)])], [PulseOffice.Foundation.Units.Twip.FromPoints(72)]));
        var transfer = new DeckRichTextTransfer(new FixedInterchange(document));

        var paragraphs = transfer.Import(null, "<p>x</p>")!;

        Assert.Equal(["intro", "cell"], paragraphs.Select(paragraph => paragraph.Text));
    }
}
