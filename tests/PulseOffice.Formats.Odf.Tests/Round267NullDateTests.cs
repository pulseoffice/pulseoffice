using System.IO.Compression;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round267NullDateTests
{
    [Fact]
    public void A_null_date_other_than_the_standard_one_crosses_ODS_with_its_serials()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Calculation = workbook.Calculation with { DateEpoch = CalculationSettings.Mac1904DateEpoch };
        var date = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("YYYY-MM-DD") });
        workbook.Sheets[0][new CellAddress(0, 0)] = new Cell { Value = CellValue.Number(1), StyleId = date, ExplicitStyle = true };

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
            var content = reader.ReadToEnd();
            Assert.Contains("table:null-date", content);
            Assert.Contains("office:date-value=\"1904-01-02\"", content);
        }
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;

        Assert.Equal(CalculationSettings.Mac1904DateEpoch, read.Calculation.DateEpoch);
        Assert.Equal(1, read.Sheets[0].GetValue(new CellAddress(0, 0)).AsNumber());
    }
}
