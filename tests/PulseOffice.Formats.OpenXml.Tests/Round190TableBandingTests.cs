using PulseOffice.Document.Tables;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round190TableBandingTests
{
    private const uint Band = 0xFFDBEAFE;

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly TableSample _sample = new();
    private readonly TableAppearance _appearance = new(new StyleResolver());
    private readonly DocxTableLooks _looks = new();

    private static readonly TableBanding Everything = new()
    {
        BandedRows = false, BandedColumns = true, FirstColumn = true, LastColumn = true, LastRow = true,
    };

    private static readonly TableProperties Styled = new() { BandBackground = Band, HeaderBold = true };

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    private TableBlock Table(TextDocument document) => document.Body.Blocks.OfType<TableBlock>().Single();

    [Theory]
    [InlineData("odt")]
    [InlineData("docx")]
    public void Table_style_options_cross_both_formats(string format)
    {
        var document = _sample.Build();
        Table(document).Banding = Everything;

        var read = format == "odt"
            ? Through(document, new OdtWriter(), new OdtReader())
            : Through(document, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));

        Assert.Equal(Everything, Table(read).Banding);
    }

    [Fact]
    public void A_word_table_turning_banding_off_shows_no_bands()
    {
        Assert.False(_looks.Read(new W.TableLook { NoHorizontalBand = true }).BandedRows);
        Assert.False(_looks.Read(new W.TableLook { Val = "0220" }).BandedRows);
        Assert.True(_looks.Read(new W.TableLook { Val = "04A0" }).BandedRows);

        var table = Table(_sample.Build());
        Assert.Equal(Band, _appearance.Cell(table, Styled, 2, 1).Background);
        table.Banding = TableBanding.None;
        Assert.Null(_appearance.Cell(table, Styled, 2, 1).Background);
    }

    [Fact]
    public void Emphasis_and_banded_columns_are_drawn()
    {
        var table = Table(_sample.Build());
        table.Banding = Everything;

        Assert.True(_appearance.Cell(table, Styled, 4, 1).Bold);
        Assert.False(_appearance.Cell(table, Styled, 3, 1).Bold);
        Assert.Equal(Band, _appearance.Cell(table, Styled, 3, 1).Background);
    }
}
