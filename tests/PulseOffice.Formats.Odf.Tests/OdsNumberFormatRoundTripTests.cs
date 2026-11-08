using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsNumberFormatRoundTripTests
{
    private static (string Code, CellValue Value) RoundTrip(string code, double number)
    {
        var source = Workbook.CreateDefault();
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        var id = source.NumberFormats.Register(code);
        source.Sheets[0].SetValue(a1, CellValue.Number(number));
        source.Sheets[0].SetStyle(a1, source.Styles.Intern(new CellStyle { NumberFormatId = id }));

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var read = result.Value!;
        var cell = read.Sheets[0][a1];
        return (read.NumberFormats.CodeOf(read.Styles.Get(cell.StyleId).NumberFormatId), cell.Value);
    }

    [Theory]
    [InlineData("dd/mm/yyyy", 46000)]
    [InlineData("dd/mm/yyyy h:mm", 46000.5)]
    [InlineData("dddd d mmmm yyyy", 46000)]
    [InlineData("h:mm:ss", 0.25)]
    [InlineData("[h]:mm:ss", 1.5)]
    [InlineData("mm:ss.0", 0.001)]
    [InlineData("#,##0.00", 1234.5)]
    [InlineData("0.00%", 0.125)]
    [InlineData("0.00E+00", 12345)]
    [InlineData("# ?/?", 1.5)]
    [InlineData("#,##0;[RED]-#,##0", -5)]
    [InlineData("[$€-410] #,##0.00", 3.5)]
    [InlineData("0.000\" kg\"", 2)]
    [InlineData("@", 7)]
    public void IlFormatoNumericoEIlValoreSopravvivono(string code, double number)
    {
        var (readCode, value) = RoundTrip(code, number);
        Assert.Equal(code, readCode);
        Assert.Equal(number, value.AsNumber(), 6);
    }
}
