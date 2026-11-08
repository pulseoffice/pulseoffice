using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;
using ModelCell = PulseOffice.Sheets.Cell;
using ModelValue = PulseOffice.Sheets.Values.CellValue;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxCalcErrorAndLeapYearTests
{
    private static readonly CellAddress First = new(0, 0);

    private static MemoryStream Xlsx(Book workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private static Book ThroughOds(Book workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Require();
    }

    private static string RawValue(MemoryStream xlsx, out string? type)
    {
        using var document = SpreadsheetDocument.Open(xlsx, false);
        var cell = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<Cell>().First();
        type = cell.DataType?.Value.ToString();
        xlsx.Position = 0;
        return cell.CellValue!.Text;
    }

    private static MemoryStream WithRawSerial(Book workbook, string serial)
    {
        var stream = Xlsx(workbook);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            worksheet.Descendants<Cell>().First().CellValue!.Text = serial;
            worksheet.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private static Book Dated(double serial, string code = "yyyy-mm-dd")
    {
        var workbook = Book.CreateDefault();
        var style = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register(code) });
        workbook.Sheets[0][First] = new ModelCell { Value = ModelValue.Number(serial), StyleId = style, ExplicitStyle = true };
        return workbook;
    }

    [Fact]
    public void Calc_error_is_written_as_an_excel_error_and_read_back()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].SetValue(First, ModelValue.FromError(CellError.Calc));
        workbook.Sheets[0].SetValue(new CellAddress(1, 0), ModelValue.FromError(CellError.Spill));

        using var xlsx = Xlsx(workbook);
        Assert.Equal("#CALC!", RawValue(xlsx, out var type));
        Assert.Equal(CellValues.Error.ToString(), type);

        var read = new XlsxReader().Read(xlsx).Require();
        Assert.Equal(CellError.Calc, read.Sheets[0].GetValue(First).Error);
        Assert.Equal(CellError.Spill, read.Sheets[0].GetValue(new CellAddress(1, 0)).Error);
    }

    [Fact]
    public void Constant_errors_outside_the_excel_formula_grammar_carry_no_formula()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].SetValue(First, ModelValue.FromError(CellError.Calc));
        workbook.Sheets[0].SetValue(new CellAddress(1, 0), ModelValue.FromError(CellError.Spill));
        workbook.Sheets[0].SetValue(new CellAddress(2, 0), ModelValue.FromError(CellError.NotAvailable));

        using var xlsx = Xlsx(workbook);
        using (var document = SpreadsheetDocument.Open(xlsx, false))
        {
            var cells = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<Cell>().ToList();
            Assert.Null(cells[0].CellFormula);
            Assert.Null(cells[1].CellFormula);
            Assert.Equal("#N/A", cells[2].CellFormula?.Text);
        }
        xlsx.Position = 0;
        var read = new XlsxReader().Read(xlsx).Require();
        Assert.Equal(CellError.Calc, read.Sheets[0].GetValue(First).Error);
        Assert.Null(read.Sheets[0][First].Formula);
    }

    [Fact]
    public void Calc_error_crosses_ods_and_the_difference_is_declared()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].SetValue(First, ModelValue.FromError(CellError.Calc));

        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF271");
        var fromOds = ThroughOds(workbook);
        Assert.Equal(CellError.Calc, fromOds.Sheets[0].GetValue(First).Error);

        using var xlsx = Xlsx(fromOds);
        var back = ThroughOds(new XlsxReader().Read(xlsx).Require());
        Assert.Equal(CellError.Calc, back.Sheets[0].GetValue(First).Error);
    }

    [Fact]
    public void Formula_giving_calc_keeps_the_formula_and_the_cached_error_in_both_formats()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].SetFormula(First, "=SEQUENCE(0)");
        workbook.Sheets[0][First] = workbook.Sheets[0][First] with { Value = ModelValue.FromError(CellError.Calc) };

        using var xlsx = Xlsx(workbook);
        var fromXlsx = new XlsxReader().Read(xlsx).Require();
        Assert.Equal(CellError.Calc, fromXlsx.Sheets[0].GetValue(First).Error);
        Assert.Equal(CellError.Calc, ThroughOds(fromXlsx).Sheets[0].GetValue(First).Error);
    }

    [Theory]
    [InlineData(2, "1")]
    [InlineData(16, "15")]
    [InlineData(60, "59")]
    [InlineData(61, "61")]
    [InlineData(45000, "45000")]
    public void Dates_before_march_1900_are_written_with_excel_serials(double serial, string excel)
    {
        using var xlsx = Xlsx(Dated(serial));
        Assert.Equal(excel, RawValue(xlsx, out _));
        Assert.Equal(serial, new XlsxReader().Read(xlsx).Require().Sheets[0].GetValue(First).AsNumber());
    }

    [Theory]
    [InlineData("1", 2)]
    [InlineData("15.25", 16.25)]
    [InlineData("59", 60)]
    [InlineData("61", 61)]
    [InlineData("0.5", 0.5)]
    public void Excel_serials_before_march_1900_are_read_as_the_same_calendar_day(string excel, double expected)
    {
        using var xlsx = WithRawSerial(Dated(100), excel);
        Assert.Equal(expected, new XlsxReader().Read(xlsx).Require().Sheets[0].GetValue(First).AsNumber());
    }

    [Fact]
    public void Excel_fictitious_29_february_is_read_as_28_february_and_declared()
    {
        using var xlsx = WithRawSerial(Dated(100), "60");
        var result = new XlsxReader().Read(xlsx);
        Assert.Equal(60, result.Require().Sheets[0].GetValue(First).AsNumber());
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "XLSX191");
    }

    [Fact]
    public void Numbers_and_times_are_not_shifted()
    {
        var plain = Book.CreateDefault();
        plain.Sheets[0].SetValue(First, ModelValue.Number(16));
        using (var xlsx = Xlsx(plain))
        {
            Assert.Equal("16", RawValue(xlsx, out _));
            Assert.Equal(16, new XlsxReader().Read(xlsx).Require().Sheets[0].GetValue(First).AsNumber());
        }

        using var time = Xlsx(Dated(16.5, "hh:mm"));
        Assert.Equal("16.5", RawValue(time, out _));
    }

    [Fact]
    public void An_early_date_crosses_xlsx_and_ods_as_the_same_day()
    {
        var original = Dated(16);
        using var xlsx = Xlsx(original);
        var fromXlsx = new XlsxReader().Read(xlsx).Require();
        var fromOds = ThroughOds(fromXlsx);
        Assert.Equal(16, fromOds.Sheets[0].GetValue(First).AsNumber());

        using var again = Xlsx(fromOds);
        Assert.Equal("15", RawValue(again, out _));
    }

    [Fact]
    public void Dates_before_excel_calendar_are_declared()
    {
        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(Dated(1.5)).Losses, loss => loss.Code == "XLSX192");
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(Dated(16)).Losses, loss => loss.Code == "XLSX192");
    }
}
