using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round285TableRegionClearStyleTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static readonly BorderLine Rule = new(1, 0xFF336699);

    private static readonly TableProperties Cleared = new()
    {
        HeaderRow = new TableRegionFormat { Italic = true, Borders = new BoxBorders { Bottom = new BorderLine(0, 0xFF000000), Top = Rule } },
        TotalRow = new TableRegionFormat { Font = string.Empty, FontSize = 0, Bold = true },
    };

    private MemoryStream Written(ITextDocumentWriter writer)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Table.Add(new TableStyle("Cleared") { Table = Cleared });
        var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_zero_width_region_line_crosses_docx_as_an_explicit_nil_side()
    {
        using var stream = Written(_composer.Writer(DocxFlavour.Document));

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!.Styles.Table.Find("Cleared")!.Table;

        Assert.Equal(0, read.HeaderRow!.Borders!.Bottom!.Width);
        Assert.Equal(Rule, read.HeaderRow.Borders.Top);
        Assert.Null(read.HeaderRow.Borders.Left);
    }

    [Fact]
    public void Zero_width_lines_and_cleared_fonts_cross_odt()
    {
        using var stream = Written(new OdtWriter());

        var read = new OdtReader().Read(stream).Value!.Styles.Table.Find("Cleared")!.Table;

        Assert.Equal(0, read.HeaderRow!.Borders!.Bottom!.Width);
        Assert.Equal(Rule, read.HeaderRow.Borders.Top);
        Assert.Null(read.HeaderRow.Borders.Left);
        Assert.Equal(string.Empty, read.TotalRow!.Font);
        Assert.Equal(0, read.TotalRow.FontSize);
    }
}
