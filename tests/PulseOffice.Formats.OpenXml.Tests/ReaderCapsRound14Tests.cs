using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Document.Embedding;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

[Collection(nameof(OpenXmlLargeDocumentPerformanceTests))]
public sealed class ReaderCapsRound14Tests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private P.GraphicFrame Frame(int gridColumns, int rows, int cellsPerRow)
    {
        var table = new D.Table(new D.TableProperties());
        var grid = new D.TableGrid();
        for (var c = 0; c < gridColumns; c++) grid.Append(new D.GridColumn { Width = 1000 });
        table.Append(grid);
        for (var r = 0; r < rows; r++)
        {
            var row = new D.TableRow { Height = 1000 };
            for (var c = 0; c < cellsPerRow; c++) row.Append(new D.TableCell(new D.TextBody(new D.BodyProperties(), new D.Paragraph())));
            table.Append(row);
        }
        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(new P.NonVisualDrawingProperties { Id = 2, Name = "Table" }, new P.NonVisualGraphicFrameDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(new D.Offset { X = 0, Y = 0 }, new D.Extents { Cx = 1000, Cy = 1000 }),
            new D.Graphic(new D.GraphicData(table) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));
    }

    [Theory]
    [InlineData(3000, 3000, 0)]
    [InlineData(1, 1, 5000)]
    [InlineData(1, 5000, 1)]
    public void Una_tabella_PPTX_con_righe_e_colonne_a_dismisura_resta_entro_i_tetti_di_Deck(int gridColumns, int rows, int cellsPerRow)
    {
        var element = new PptxDeckTableMapper().Read(Frame(gridColumns, rows, cellsPerRow), "t")!;
        var table = (DataTableObject)element.Content;

        Assert.InRange(table.Snapshot.Columns.Count, 1, ImportedTableLimits.SlideTableColumns);
        Assert.InRange(table.Snapshot.Rows.Count, 1, ImportedTableLimits.SlideTableRows);
        Assert.InRange((long)table.Snapshot.Rows.Count * table.Snapshot.Columns.Count, 1, ImportedTableLimits.SlideTableCells);
        Assert.All(table.Snapshot.Rows, row => Assert.Equal(table.Snapshot.Columns.Count, row.Count));
        Assert.InRange(table.Layout.RowHeights.Count, 0, table.Snapshot.Rows.Count + 1);
        Assert.InRange(table.Layout.ColumnFractions.Count, 0, table.Snapshot.Columns.Count);
    }

    private TextDocument Docx(Func<W.Body> body)
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document(body());
        source.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(source).Value!;
    }

    private W.Table EmptyRows(int gridColumns, int rows)
    {
        var table = new W.Table(new W.TableProperties());
        var grid = new W.TableGrid();
        for (var c = 0; c < gridColumns; c++) grid.Append(new W.GridColumn { Width = "100" });
        table.Append(grid);
        for (var r = 0; r < rows; r++) table.Append(new W.TableRow());
        return table;
    }

    [Fact]
    public void Una_griglia_DOCX_di_migliaia_di_colonne_resta_entro_il_tetto_delle_tabelle_di_testo()
    {
        var table = Docx(() => new W.Body(EmptyRows(5000, 2))).Body.Blocks.OfType<TableBlock>().Single();

        Assert.InRange(table.Columns.Count, 1, ImportedTableLimits.TextTableColumns);
        Assert.All(table.Rows, row => Assert.Equal(table.Columns.Count, row.Cells.Count));
    }

    [Fact]
    public void Righe_DOCX_vuote_moltiplicate_per_la_griglia_restano_entro_il_tetto_di_celle()
    {
        var table = Docx(() => new W.Body(EmptyRows(1024, 1200))).Body.Blocks.OfType<TableBlock>().Single();

        Assert.InRange((long)table.Rows.Count * table.Columns.Count, 1, ImportedTableLimits.TextTableCells);
        Assert.All(table.Rows, row => Assert.Equal(table.Columns.Count, row.Cells.Count));
    }

    [Fact]
    public void Molte_tabelle_DOCX_larghe_restano_entro_il_tetto_di_celle_del_documento_e_la_perdita_e_dichiarata()
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document(new W.Body(EmptyRows(1024, 700), new W.Paragraph(), EmptyRows(1024, 700), new W.Paragraph(), EmptyRows(1024, 700)));
        source.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(source);

        var cells = read.Value!.Body.Blocks.OfType<TableBlock>().Sum(table => (long)table.Rows.Count * table.Columns.Count);
        Assert.InRange(cells, 1, ImportedTableLimits.DocumentTableCells);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == DocxBlockReader.TruncatedTable);
    }

    [Fact]
    public void Colonne_XLSX_ripetute_su_tutto_il_foglio_si_leggono_in_tempo_lineare()
    {
        var workbook = new PulseOffice.Sheets.Workbook();
        workbook.AddSheet("Dati");
        var source = new MemoryStream();
        new XlsxWriter().Write(workbook, source);
        using (var package = SpreadsheetDocument.Open(source, true))
        {
            var worksheet = package.WorkbookPart!.WorksheetParts.First().Worksheet!;
            worksheet.RemoveAllChildren<DocumentFormat.OpenXml.Spreadsheet.Columns>();
            var columns = new DocumentFormat.OpenXml.Spreadsheet.Columns();
            for (var i = 0; i < 3000; i++)
                columns.Append(new DocumentFormat.OpenXml.Spreadsheet.Column { Min = 1, Max = 16384, Width = 20 + i % 7, CustomWidth = true });
            worksheet.InsertBefore(columns, worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.SheetData>());
        }
        source.Position = 0;

        var watch = Stopwatch.StartNew();
        var read = new XlsxReader().Read(source);
        watch.Stop();

        Assert.True(read.Succeeded);
        Assert.InRange(watch.Elapsed.TotalSeconds, 0, 5);
    }

    [Fact]
    public void Molte_celle_DOCX_con_bordi_propri_si_leggono_in_tempo_lineare()
    {
        W.TableCell Cell() => new(
            new W.TableCellProperties(new W.TableCellBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4 })),
            new W.Paragraph());
        var table = new W.Table(new W.TableProperties(), new W.TableGrid(Enumerable.Range(0, 40).Select(_ => new W.GridColumn { Width = "300" })));
        for (var r = 0; r < 6000; r++) table.Append(new W.TableRow(Enumerable.Range(0, 40).Select(_ => Cell())));

        var watch = Stopwatch.StartNew();
        var read = Docx(() => new W.Body(table)).Body.Blocks.OfType<TableBlock>().Single();
        watch.Stop();

        Assert.Equal(6000, read.Rows.Count);
        Assert.NotNull(read.Rows[^1].Cells[^1].Borders?.Top);
        Assert.InRange(watch.Elapsed.TotalSeconds, 0, 20);
    }
}
