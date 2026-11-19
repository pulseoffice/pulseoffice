using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using ModelValue = PulseOffice.Sheets.Values.CellValue;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Xlsx1904DateSystemTests
{
    [Fact]
    public void A_1904_workbook_keeps_its_serials_and_its_date_system()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var date = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("dd/mm/yyyy") });
        var time = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("hh:mm") });
        sheet[new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(43831), StyleId = date, ExplicitStyle = true };
        sheet[new CellAddress(1, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(0.5), StyleId = time, ExplicitStyle = true };
        sheet[new CellAddress(2, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(43831) };

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var book = document.WorkbookPart!.Workbook!;
            var properties = book.WorkbookProperties ?? book.InsertAt(new WorkbookProperties(), 0);
            properties.Date1904 = true;
            book.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();

        Assert.Equal(43831, read.Sheets[0].GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.Equal(0.5, read.Sheets[0].GetValue(new CellAddress(1, 0)).AsNumber());
        Assert.Equal(43831, read.Sheets[0].GetValue(new CellAddress(2, 0)).AsNumber());
        Assert.Equal(PulseOffice.Sheets.CalculationSettings.Mac1904DateEpoch, read.Calculation.DateEpoch);

        using var again = new MemoryStream();
        new XlsxWriter().Write(read, again);
        again.Position = 0;
        using (var document = SpreadsheetDocument.Open(again, false))
            Assert.True(document.WorkbookPart!.Workbook!.WorkbookProperties?.Date1904?.Value);
        again.Position = 0;
        Assert.Equal(43831, new XlsxReader().Read(again).Require().Sheets[0].GetValue(new CellAddress(0, 0)).AsNumber());
    }

    [Fact]
    public void Calendar_formats_of_two_workbooks_are_not_confused()
    {
        var first = PulseOffice.Sheets.Workbook.CreateDefault();
        var dated = first.Styles.Intern(first.Styles.Get(0) with { NumberFormatId = first.NumberFormats.Register("yyyy\"x\"mm") });
        first.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(10), StyleId = dated, ExplicitStyle = true };
        var second = PulseOffice.Sheets.Workbook.CreateDefault();
        var plain = second.Styles.Intern(second.Styles.Get(0) with { NumberFormatId = second.NumberFormats.Register("0.000") });
        second.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(10), StyleId = plain, ExplicitStyle = true };
        Assert.Equal(first.Styles.Get(dated).NumberFormatId, second.Styles.Get(plain).NumberFormatId);

        Assert.Single(new XlsxCalendarCells(first).ConstantDates());
        Assert.Empty(new XlsxCalendarCells(second).ConstantDates());
    }

    [Theory]
    [InlineData("a\u0001b")]
    [InlineData("a\uD800b")]
    public void Un_testo_con_caratteri_di_controllo_passa_da_XLSX_a_ODS(string text)
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Text(text) };
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var read = new XlsxReader().Read(xlsx).Require();

        using var ods = new MemoryStream();
        new PulseOffice.Formats.Odf.OdsWriter().Write(read, ods);
        ods.Position = 0;
        Assert.NotNull(new PulseOffice.Formats.Odf.OdsReader().Read(ods).Value);
    }

    [Fact]
    public void Margini_scala_e_interruzioni_fuori_misura_restano_nei_limiti()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(1) };
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            var margins = worksheet.GetFirstChild<PageMargins>()!;
            margins.Left = double.NaN;
            margins.Right = 1e300;
            margins.Header = -4;
            worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.PageSetup>()!.Scale = 4_000_000u;
            worksheet.Append(new RowBreaks(new Break { Id = uint.MaxValue, Max = 16383, ManualPageBreak = true }));
            worksheet.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();
        var setup = read.PageStyles.For(read.Sheets[0]).Setup;

        Assert.InRange(setup.Margins.Left.Value, 0, 49 * 1440);
        Assert.InRange(setup.Margins.Right.Value, 0, 49 * 1440);
        Assert.InRange(setup.Margins.Top.Value, 0, 49 * 1440);
        Assert.InRange(setup.Scale, 0.1, 4);
        Assert.All(read.Sheets[0].RowBreaks, row => Assert.InRange(row, 1, 1048575));
    }

    [Fact]
    public void Numeri_non_finiti_e_colore_della_linguetta_non_validi_non_producono_un_file_rotto()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(double.NaN) };
        workbook.Sheets[0][new CellAddress(1, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(double.PositiveInfinity) };
        workbook.Sheets[0].TabColorHex = "#abc";
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            Assert.DoesNotContain(worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.CellValue>(), value => value.Text is "NaN" or "Infinity" or "∞");
            Assert.Empty(worksheet.Descendants<TabColor>());
        }
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();
        Assert.Equal(PulseOffice.Sheets.Values.ValueKind.Error, read.Sheets[0].GetValue(new CellAddress(0, 0)).Kind);
    }

    [Fact]
    public void Un_numero_NaN_nel_file_si_legge_come_errore()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0][new CellAddress(0, 0)] = new PulseOffice.Sheets.Cell { Value = ModelValue.Number(1) };
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.CellValue>().First().Text = "NaN";
            worksheet.Save();
        }
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();
        Assert.Equal(PulseOffice.Sheets.Values.ValueKind.Error, read.Sheets[0].GetValue(new CellAddress(0, 0)).Kind);
    }
}
