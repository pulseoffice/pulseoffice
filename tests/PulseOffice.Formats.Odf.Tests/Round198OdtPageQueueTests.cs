using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round198OdtPageQueueTests
{
    private TextDocument Document() => new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    [Fact]
    public void Columns_of_different_widths_cross_odt()
    {
        var document = Document();
        var spans = new ColumnSpan[] { new(new Twip(2000), new Twip(720)), new(new Twip(6000), Twip.Zero) };
        document.Styles.Page.Find(document.InitialPageStyle)!.Columns = new SectionColumns(2, new Twip(720)) { Spans = spans };

        var read = RoundTrip(document);

        var columns = read.Styles.Page.Find(read.InitialPageStyle)!.Columns;
        Assert.Equal([2000, 6000], columns.Spans.Select(span => span.Width.Value));
        Assert.InRange(columns.Spans[0].Gap.Value, 715, 725);
    }

    [Fact]
    public void Line_numbering_restarted_per_section_with_a_start_value_crosses_odt()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1) { RestartEachSection = true, Start = 5 };

        var read = RoundTrip(document).LineNumbering!;

        Assert.True(read.RestartEachSection);
        Assert.Equal(5, read.Start);
    }

    [Fact]
    public void A_gutter_crosses_odt_beside_the_left_margin()
    {
        var document = Document();
        var style = document.Styles.Page.Find(document.InitialPageStyle)!;
        style.Gutter = new Twip(560);

        var reread = RoundTrip(document);
        var read = reread.Styles.Page.Find(reread.InitialPageStyle)!;

        Assert.Equal(560, read.Gutter.Value);
        Assert.False(read.GutterAtTop);
        Assert.Equal(style.Margins, read.Margins);
    }
}
