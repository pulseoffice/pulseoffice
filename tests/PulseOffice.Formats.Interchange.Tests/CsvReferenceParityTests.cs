using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public class CsvReferenceParityTests
{
    private static readonly DelimitedText Delimited = new();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Worksheet Read(string file)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));
        var reader = new CsvSpreadsheetReader(Delimited, new CsvValueReader(), new CsvSniffer(Delimited));
        var result = reader.Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!.Sheets[0];
    }

    private static string Text(Worksheet sheet, string a1) => sheet.GetValue(A(a1)).ToString();

    [Fact]
    public void LeVirgoletteSpaiateSiLegonoComeCalc()
    {
        var sheet = Read("fdo48621_broken_quotes.csv");

        Assert.Equal("abc\" d \"ef", Text(sheet, "A2"));
        Assert.Equal("a\"b, \"a", Text(sheet, "A3"));
        Assert.Equal("shall be:", Text(sheet, "B3"));
    }

    [Fact]
    public void LeRigheVuoteNonSpostanoIDati()
    {
        var sheet = Read("tdf48731.csv");

        Assert.Equal(CellValue.Empty, sheet.GetValue(A("A8")));
        Assert.Equal("WITH QUOTES", Text(sheet, "A9"));
    }

    [Fact]
    public void IlRitornoACapoDentroLeVirgoletteDiventaUnaRigaNuova()
    {
        var sheet = Read("tdf152980.csv");

        Assert.Equal("a_xfoo\nb", Text(sheet, "A5"));
        Assert.Equal("a\n\nb", Text(sheet, "A8"));
    }

    [Fact]
    public void LeVirgoletteDopoUnoSpazioApronoUnCampo()
    {
        var sheet = Read("tdf80149.csv");

        Assert.Equal("File opens in libre office, but can't be saved as xlsx", Text(sheet, "C1"));
        Assert.Equal("This cell goes missing", Text(sheet, "C2"));
    }
}
