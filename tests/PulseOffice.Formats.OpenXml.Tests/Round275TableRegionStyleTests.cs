using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round275TableRegionStyleTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static readonly TableProperties Regions = new()
    {
        Background = TableRegionFormat.NoColour,
        HeaderColor = TableRegionFormat.NoColour,
        TotalRow = new TableRegionFormat { Background = 0xFFDDEEFF, Bold = true },
        FirstColumn = new TableRegionFormat { Ink = 0xFF112233 },
        LastColumn = new TableRegionFormat { Background = TableRegionFormat.NoColour },
        NorthWestCell = new TableRegionFormat { Background = 0xFF445566 },
        SouthEastCell = new TableRegionFormat { Bold = false, Ink = 0xFF778899 },
    };

    private TextDocument Document()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Table.Add(new TableStyle("Regions") { Table = Regions });
        return document;
    }

    private TableProperties Through(ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(Document(), stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!.Styles.Table.Find("Regions")!.Table;
    }

    [Fact]
    public void Total_row_column_and_corner_formats_cross_docx()
    {
        var read = Through(_composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));

        Assert.Equal(Regions.TotalRow, read.TotalRow);
        Assert.Equal(Regions.FirstColumn, read.FirstColumn);
        Assert.Equal(Regions.LastColumn, read.LastColumn);
        Assert.Equal(Regions.NorthWestCell, read.NorthWestCell);
        Assert.Equal(Regions.SouthEastCell, read.SouthEastCell);
        Assert.Equal(TableRegionFormat.NoColour, read.Background);
        Assert.Equal(TableRegionFormat.NoColour, read.HeaderColor);
    }

    [Fact]
    public void Total_row_column_and_corner_formats_cross_odt()
    {
        var read = Through(new OdtWriter(), new OdtReader());

        Assert.Equal(Regions.TotalRow, read.TotalRow);
        Assert.Equal(Regions.FirstColumn, read.FirstColumn);
        Assert.Equal(Regions.LastColumn, read.LastColumn);
        Assert.Equal(Regions.NorthWestCell, read.NorthWestCell);
        Assert.Equal(Regions.SouthEastCell, read.SouthEastCell);
        Assert.Equal(TableRegionFormat.NoColour, read.Background);
    }

    private MemoryStream Package(string table)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>" + table + "<w:p/></w:body></w:document>");
        }
        stream.Position = 0;
        return stream;
    }

    private const string Lines = "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>"
        + "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>"
        + "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>";

    private TableBlock Read(string table) =>
        _composer.Reader(DocxFlavour.Document).Read(Package(table)).Value!.Body.Blocks.OfType<TableBlock>().Single();

    [Fact]
    public void A_nil_cell_side_in_a_docx_file_removes_the_inside_line_on_both_cells()
    {
        var table = Read("<w:tbl><w:tblPr><w:tblBorders>" + Lines + "</w:tblBorders></w:tblPr>"
            + "<w:tblGrid><w:gridCol w:w=\"1440\"/><w:gridCol w:w=\"1440\"/></w:tblGrid>"
            + "<w:tr><w:tc><w:tcPr><w:tcBorders><w:right w:val=\"nil\"/></w:tcBorders></w:tcPr><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr></w:tbl>");

        Assert.Null(table.Cell(0, 0).Borders?.Right);
        Assert.Null(table.Cell(0, 1).Borders?.Left);
        Assert.NotNull(table.Cell(0, 1).Borders?.Right);
    }

    [Fact]
    public void Percent_cell_widths_share_a_percent_table_without_a_grid()
    {
        var table = Read("<w:tbl><w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/></w:tblPr><w:tblGrid/>"
            + "<w:tr><w:tc><w:tcPr><w:tcW w:w=\"1000\" w:type=\"pct\"/></w:tcPr><w:p/></w:tc><w:tc><w:tcPr><w:tcW w:w=\"4000\" w:type=\"pct\"/></w:tcPr><w:p/></w:tc></w:tr></w:tbl>");

        Assert.Equal(4.0, table.Columns[1].Value / (double)table.Columns[0].Value, 1);
    }
}
