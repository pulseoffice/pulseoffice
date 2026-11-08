using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxDirectTableBordersTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly TableAppearance _appearance = new(new StyleResolver());

    private W.BorderType Single(W.BorderType border)
    {
        border.Val = W.BorderValues.Single;
        border.Size = 4;
        return border;
    }

    private W.BorderType None(W.BorderType border)
    {
        border.Val = W.BorderValues.None;
        return border;
    }

    private MemoryStream Source()
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new W.Styles(new W.Style(
                new W.StyleName { Val = "Table Grid" },
                new W.StyleTableProperties(new W.TableBorders(
                    Single(new W.TopBorder()), Single(new W.LeftBorder()), Single(new W.BottomBorder()), Single(new W.RightBorder()),
                    Single(new W.InsideHorizontalBorder()), Single(new W.InsideVerticalBorder()))))
            { Type = W.StyleValues.Table, StyleId = "TableGrid" });
            W.TableCell Cell(string text, W.TableCellBorders? borders) =>
                new(new W.TableCellProperties(borders is null ? [] : [borders]), new W.Paragraph(new W.Run(new W.Text(text))));
            main.Document = new W.Document(new W.Body(new W.Table(
                new W.TableProperties(new W.TableStyle { Val = "TableGrid" }, new W.TableBorders(
                    None(new W.TopBorder()), None(new W.LeftBorder()), None(new W.BottomBorder()), None(new W.RightBorder()),
                    None(new W.InsideHorizontalBorder()), None(new W.InsideVerticalBorder()))),
                new W.TableGrid(new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }),
                new W.TableRow(Cell("Etichetta", null), Cell("Casella", new W.TableCellBorders(
                    Single(new W.TopBorder()), Single(new W.LeftBorder()), Single(new W.BottomBorder()), Single(new W.RightBorder())))),
                new W.TableRow(Cell("Nota", new W.TableCellBorders(Single(new W.RightBorder()))), Cell(string.Empty, null)))));
        }
        source.Position = 0;
        return source;
    }

    private TextDocument Read()
    {
        using var source = Source();
        return _composer.Reader(DocxFlavour.Document).Read(source).Value!;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    public static TheoryData<string> Routes() => new() { "docx", "docx-docx", "docx-odt" };

    private TextDocument Route(string route) => route switch
    {
        "docx" => Read(),
        "docx-docx" => Through(Read(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
        _ => Through(Read(), new OdtWriter(), new OdtReader()),
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void I_bordi_diretti_della_tabella_vincono_su_quelli_dello_stile_come_in_Word(string route)
    {
        var document = Route(route);
        var table = document.Body.Blocks.OfType<TableBlock>().Single();
        var style = _appearance.Style(document.Styles, table);

        var label = _appearance.Cell(table, style, 0, 0).Borders;
        var box = _appearance.Cell(table, style, 0, 1).Borders;
        var note = _appearance.Cell(table, style, 1, 0).Borders;

        Assert.Null(label.Top);
        Assert.Null(label.Left);
        Assert.Null(label.Bottom);
        Assert.NotNull(box.Top);
        Assert.NotNull(box.Bottom);
        Assert.NotNull(note.Right);
        Assert.Null(note.Top);
        Assert.Null(note.Left);
    }

    [Fact]
    public void In_una_fusione_verticale_il_bordo_inferiore_viene_dall_ultima_cella_fusa_come_in_Word()
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Table(
                new W.TableGrid(new W.GridColumn { Width = "2000" }),
                new W.TableRow(new W.TableCell(new W.TableCellProperties(new W.VerticalMerge { Val = W.MergedCellValues.Restart },
                    new W.TableCellBorders(Single(new W.TopBorder()), Single(new W.LeftBorder()), Single(new W.RightBorder()))), new W.Paragraph())),
                new W.TableRow(new W.TableCell(new W.TableCellProperties(new W.VerticalMerge(),
                    new W.TableCellBorders(Single(new W.LeftBorder()), Single(new W.BottomBorder()), Single(new W.RightBorder()))), new W.Paragraph())))));
        }
        source.Position = 0;

        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        var table = document.Body.Blocks.OfType<TableBlock>().Single();

        Assert.Equal(2, table.Cell(0, 0).RowSpan);
        Assert.NotNull(table.Cell(0, 0).Borders!.Bottom);
        Assert.NotNull(table.Cell(0, 0).Borders!.Top);
    }
}
