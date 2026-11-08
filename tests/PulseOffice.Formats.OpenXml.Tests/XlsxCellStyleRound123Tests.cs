using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using Xunit;
using CellStyle = PulseOffice.Sheets.Styling.CellStyle;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxCellStyleRound123Tests
{
    [Fact]
    public void A_style_name_with_surrounding_blanks_keeps_its_cell_links()
    {
        var stylesheet = new Stylesheet(
            new Fonts(new Font()), new Fills(new Fill()), new Borders(new Border()),
            new CellStyleFormats(new CellFormat { FontId = 0, FillId = 0, BorderId = 0 }, new CellFormat { FontId = 0, FillId = 0, BorderId = 0 }),
            new CellFormats(new CellFormat { FormatId = 0 }, new CellFormat { FormatId = 1 }),
            new CellStyles(
                new DocumentFormat.OpenXml.Spreadsheet.CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 },
                new DocumentFormat.OpenXml.Spreadsheet.CellStyle { Name = "  Accent  ", FormatId = 1 }));
        var reader = new XlsxStyleReader();

        Assert.Contains(reader.ReadNamedStyles(stylesheet), named => named.Name == "Accent");
        Assert.Equal([null, "Accent"], reader.ReadStyleNames(stylesheet));
    }

    [Fact]
    public void The_default_style_is_written_first_even_when_it_is_not_first_in_the_catalog()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Accent", CellStyle.Default with { BackgroundHex = "#FFE08A" });
        var entries = workbook.NamedStyles.All.ToList();
        var root = entries.Single(named => named.Name == "Default");
        entries.Remove(root);
        entries.Add(root);
        workbook.NamedStyles.ReplaceAll(entries);

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!.CellStyles!.Elements<DocumentFormat.OpenXml.Spreadsheet.CellStyle>().ToList();

        Assert.Equal(0u, styles.Single(style => style.BuiltinId?.Value == 0).FormatId!.Value);
        Assert.Single(styles, style => style.FormatId!.Value == 0);
    }

    [Fact]
    public void A_catalog_without_a_default_style_still_writes_the_normal_style_first()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.ReplaceAll([.. workbook.NamedStyles.All.Where(named => named.Name != "Default")]);

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!.CellStyles!.Elements<DocumentFormat.OpenXml.Spreadsheet.CellStyle>().ToList();

        Assert.Equal(0u, styles.Single(style => style.BuiltinId?.Value == 0).FormatId!.Value);
    }
}
