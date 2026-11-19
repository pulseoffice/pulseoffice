using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round259TableGridTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private W.TableCell Cell(string width, W.TableWidthUnitValues type, int span = 1)
    {
        var properties = new W.TableCellProperties(new W.TableCellWidth { Width = width, Type = type });
        if (span > 1) properties.Append(new W.GridSpan { Val = span });
        return new W.TableCell(properties, new W.Paragraph(new W.Run(new W.Text("x"))));
    }

    private TableBlock Read(W.TableGrid? grid, W.TableWidth? width, params W.TableRow[] rows)
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var table = new W.Table(new W.TableProperties(width ?? new W.TableWidth { Type = W.TableWidthUnitValues.Auto, Width = "0" }));
            if (grid is not null) table.Append(grid);
            table.Append(rows);
            package.AddMainDocumentPart().Document = new W.Document(new W.Body(table));
        }
        source.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(source).Value!.Body.Blocks.OfType<TableBlock>().Single();
    }

    [Fact]
    public void A_missing_grid_takes_the_cell_widths()
    {
        var table = Read(null, null, new W.TableRow(Cell("1000", W.TableWidthUnitValues.Dxa), Cell("3000", W.TableWidthUnitValues.Dxa)));

        Assert.Equal([1000, 3000], table.Columns.Select(column => column.Value));
    }

    [Fact]
    public void Zero_grid_columns_take_percent_widths_of_a_fixed_table()
    {
        var grid = new W.TableGrid(new W.GridColumn { Width = "0" }, new W.GridColumn { Width = "0" });
        var table = Read(grid, new W.TableWidth { Type = W.TableWidthUnitValues.Dxa, Width = "8000" },
            new W.TableRow(Cell("1250", W.TableWidthUnitValues.Pct), Cell("3750", W.TableWidthUnitValues.Pct)));

        Assert.Equal([2000, 6000], table.Columns.Select(column => column.Value));
    }
}
