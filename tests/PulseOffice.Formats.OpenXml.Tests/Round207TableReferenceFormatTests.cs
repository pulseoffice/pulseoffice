using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using X = DocumentFormat.OpenXml.Spreadsheet;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round207TableReferenceFormatTests
{
    private static readonly CellAddress Total = new(0, 3);

    private Workbook Sales(bool textHeader = true)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Name"));
        sheet.SetValue(new CellAddress(0, 1), textHeader ? CellValue.Text("Amount") : CellValue.Number(2024));
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text("n" + row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row * 10));
        }
        sheet.SetFormula(Total, textHeader ? "=SUM(Sales[Amount])" : "=SUM(Sales[2024])");
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(CellAddress.First, new CellAddress(3, 1)) });
        return workbook;
    }

    private MemoryStream Xlsx(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private MemoryStream Ods(Workbook workbook)
    {
        var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private string XlsxFormula(Workbook workbook)
    {
        using var stream = Xlsx(workbook);
        using var document = SpreadsheetDocument.Open(stream, false);
        return document.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<X.Cell>()
            .Single(cell => cell.CellReference?.Value == "D1").CellFormula!.Text;
    }

    [Fact]
    public void Xlsx_writes_and_reads_the_structured_reference()
    {
        Assert.Equal("SUM(Sales[Amount])", XlsxFormula(Sales()));

        using var stream = Xlsx(Sales());
        var read = new XlsxReader().Read(stream).Value!;

        Assert.Equal("=SUM(Sales[Amount])", read.Sheets[0][Total].Formula);
    }

    [Fact]
    public void Xlsx_keeps_this_row_references_in_the_file_form()
    {
        var workbook = Sales();
        workbook.Sheets[0].SetFormula(Total, "=Sales[[#This Row],[Amount]]+Sales[[#Headers],[Name]]");

        Assert.Equal("Sales[[#This Row],[Amount]]+Sales[[#Headers],[Name]]", XlsxFormula(workbook));
    }

    [Fact]
    public void Xlsx_flattens_the_references_to_a_table_it_cannot_write()
    {
        Assert.Equal("SUM($B$2:$B$4)", XlsxFormula(Sales(textHeader: false)));
    }

    [Fact]
    public void Ods_writes_the_resolved_range_and_reads_the_structured_reference_back()
    {
        using var stream = Ods(Sales());
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
            var content = reader.ReadToEnd();
            Assert.Contains("of:=SUM([.$B$2:.$B$4])", content, StringComparison.Ordinal);
        }
        stream.Position = 0;

        var read = new OdsReader().Read(stream).Value!;

        Assert.Equal("=SUM(Sales[Amount])", read.Sheets[0][Total].Formula);
        Assert.NotNull(read.DatabaseRanges.Find("Sales"));
    }
}
