using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using ModelValue = PulseOffice.Sheets.Values.CellValue;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round267DateEpochTests
{
    private static Workbook Dated(DateOnly epoch, double serial)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Calculation = workbook.Calculation with { DateEpoch = epoch };
        var date = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("yyyy-mm-dd") });
        workbook.Sheets[0][new CellAddress(0, 0)] = new Cell { Value = ModelValue.Number(serial), StyleId = date, ExplicitStyle = true };
        return workbook;
    }

    [Fact]
    public void An_epoch_Excel_does_not_know_is_written_in_the_1900_system_and_declared()
    {
        var workbook = Dated(new DateOnly(1900, 1, 1), 100);
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
            Assert.Null(document.WorkbookPart!.Workbook!.WorkbookProperties?.Date1904);
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();

        Assert.Equal(102, read.Sheets[0].GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX245");
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(Dated(CalculationSettings.Mac1904DateEpoch, 0)).Losses, loss => loss.Code is "XLSX245" or "XLSX192");
    }

    [Fact]
    public void A_1904_serial_below_sixty_is_not_moved_by_the_1900_leap_day()
    {
        var workbook = Dated(CalculationSettings.Mac1904DateEpoch, 30);
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();

        Assert.Equal(30, read.Sheets[0].GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.Equal(CalculationSettings.Mac1904DateEpoch, read.Calculation.DateEpoch);
    }
}
