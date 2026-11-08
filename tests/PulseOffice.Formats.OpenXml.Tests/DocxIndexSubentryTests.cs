using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxIndexSubentryTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Document()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p", "Testo")
        {
            Marks =
            [
                new IndexEntryMark(0, "Mela: renetta", MarkEdge.Point) { Key1 = "Frutta", Key2 = "Pomacee" },
                new IndexEntryMark(2, "Cane", MarkEdge.Point),
            ],
        }]);
        return document;
    }

    private IReadOnlyList<IndexEntryMark> Entries(TextDocument document) =>
        [.. Assert.IsType<ParagraphBlock>(document.Body[0]).Marks.OfType<IndexEntryMark>()];

    [Fact]
    public void LeSottovociAttraversanoIlDocxComeCampiXeALivelli()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Document(), stream).Succeeded);
        stream.Position = 0;
        var entries = Entries(_composer.Reader(DocxFlavour.Document).Read(stream).Value!);

        Assert.Equal(("Frutta", "Pomacee", "Mela: renetta"), (entries[0].Key1, entries[0].Key2, entries[0].Entry));
        Assert.Equal((null, null, "Cane"), (entries[1].Key1, entries[1].Key2, entries[1].Entry));
    }

    [Fact]
    public void LeSottovociAttraversanoLOdtConLeChiavi()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Document(), stream).Succeeded);
        stream.Position = 0;
        var entries = Entries(new OdtReader().Read(stream).Value!);

        Assert.Equal(("Frutta", "Pomacee", "Mela: renetta"), (entries[0].Key1, entries[0].Key2, entries[0].Entry));
        Assert.Null(entries[1].Key1);
    }
}
