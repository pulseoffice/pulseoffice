using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round315OdtColumnTests
{
    [Fact]
    public void Column_separator_line_and_next_column_start_cross_odt()
    {
        var line = new ColumnSeparatorLine(BorderLineStyle.Dashed, 1.5, 0xFF2563EB) { HeightPercent = 60, Alignment = ColumnSeparatorAlignment.Middle };
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([new ParagraphBlock("sp", "inside", DefaultStyleNames.Standard)]))
        {
            Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { Separator = true, SeparatorLine = line },
            StartsInNextColumn = true,
        });

        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var section = new OdtReader().Read(stream).Value!.Body.Blocks.OfType<SectionBlock>().Single();

        Assert.Equal(line, section.Columns.SeparatorLine);
        Assert.True(section.Columns.Separator);
        Assert.True(section.StartsInNextColumn);
    }
}
