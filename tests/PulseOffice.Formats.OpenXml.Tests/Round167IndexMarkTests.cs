using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round167IndexMarkTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Document()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p", "Testo")
        {
            Marks =
            [
                new IndexEntryMark(0, "Chapter entry", MarkEdge.Point) { Index = "toc", Level = 2 },
                new IndexEntryMark(2, "Term", MarkEdge.Point) { Index = "Glossary" },
                new IndexEntryMark(3, "Plain", MarkEdge.Point),
            ],
        }]);
        return document;
    }

    private IReadOnlyList<(string Index, string Entry, int? Level)> Entries(TextDocument document) =>
        [.. Assert.IsType<ParagraphBlock>(document.Body[0]).Marks.OfType<IndexEntryMark>().Select(mark => (mark.Index, mark.Entry, mark.Level))];

    [Fact]
    public void Contents_and_user_index_marks_keep_their_index_and_level_through_docx()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Document(), stream).Succeeded);
        stream.Position = 0;

        Assert.Equal([("toc", "Chapter entry", 2), ("Glossary", "Term", null), ("alphabetical", "Plain", null)],
            Entries(_composer.Reader(DocxFlavour.Document).Read(stream).Value!));
    }

    [Fact]
    public void Contents_marks_keep_their_level_through_odt()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Document(), stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(("toc", "Chapter entry", 2), Entries(new OdtReader().Read(stream).Value!)[0]);
    }
}
