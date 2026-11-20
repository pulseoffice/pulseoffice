using System.IO.Compression;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round320OdtNextColumnBreakTests
{
    private static MemoryStream Written(bool ownBreak)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var first = new ParagraphBlock("sp", "inside", DefaultStyleNames.Standard);
        if (ownBreak) first.Content = first.Content with { ColumnBreakBefore = true };
        document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([first]))
        {
            Columns = new SectionColumns(2, Twip.FromCentimeters(1)),
            StartsInNextColumn = true,
        });
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_next_column_section_writes_a_native_column_break_on_its_first_paragraph()
    {
        using var stream = Written(false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());

        Assert.Contains("fo:break-before=\"column\"", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_written_break_reads_back_as_the_section_start_only(bool ownBreak)
    {
        using var stream = Written(ownBreak);

        var section = new OdtReader().Read(stream).Value!.Body.Blocks.OfType<SectionBlock>().Single();

        Assert.True(section.StartsInNextColumn);
        Assert.Equal(ownBreak ? true : null, ((ParagraphBlock)section.Content.Blocks[0]).Content.ColumnBreakBefore);
    }

    [Fact]
    public void Page_columns_balanced_before_a_section_cross_odt()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var page = document.Styles.Page.Find(document.InitialPageStyle)!;
        page.Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { BalancedBeforeSection = true };
        document.Body.Add(new ParagraphBlock("p", "text", DefaultStyleNames.Standard));
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;

        var back = new OdtReader().Read(stream).Value!;

        Assert.True(back.Styles.Page.Find(back.InitialPageStyle)!.Columns.BalancedBeforeSection);
    }
}
