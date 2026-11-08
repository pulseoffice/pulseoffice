using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsThirdPartyFileTests
{
    private const string CalcPassword = "1012345678901234567890123456789012345678901234567890";

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Read(Stream stream)
    {
        var result = new OdsSpreadsheetReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook Fixture(string name)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Calc", name));
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return Read(copy);
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        var stream = new MemoryStream();
        Assert.True(new OdsSpreadsheetWriter().Write(workbook, stream).Succeeded);
        stream.Position = 0;
        return Read(stream);
    }

    [Theory]
    [InlineData("protection-key1.fods")]
    [InlineData("protection-key2.fods")]
    [InlineData("protection-key3.fods")]
    [InlineData("protection-key4.fods")]
    [InlineData("protection-key5.fods")]
    public void LeChiaviDiProtezioneDiCalcSiVerificanoESopravvivonoAlSalvataggio(string file)
    {
        var workbook = Fixture(file);
        var sheet = workbook.Sheets[0];

        Assert.True(sheet.Protection.IsEnabled);
        Assert.True(sheet.Protection.Accepts(CalcPassword));
        Assert.False(sheet.Protection.Accepts("sbagliata"));
        Assert.True(workbook.Protection.Accepts(CalcPassword));

        var reread = RoundTrip(workbook);
        Assert.True(reread.Sheets[0].Protection.Accepts(CalcPassword));
        Assert.False(reread.Sheets[0].Protection.Accepts("sbagliata"));
        Assert.True(reread.Protection.Structure);
        Assert.True(reread.Protection.Accepts(CalcPassword));
    }

    [Fact]
    public void UnOdsPiattoSiRiconosceESiLegge()
    {
        const string flat = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
              xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
              xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
              xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
              xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
              xmlns:of="urn:oasis:names:tc:opendocument:xmlns:of:1.2"
              office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
              <office:automatic-styles>
                <style:style style:name="ce1" style:family="table-cell"><style:text-properties fo:font-weight="bold"/></style:style>
              </office:automatic-styles>
              <office:body><office:spreadsheet>
                <table:table table:name="Dati">
                  <table:table-row>
                    <table:table-cell table:style-name="ce1" office:value-type="float" office:value="4"><text:p>4</text:p></table:table-cell>
                    <table:table-cell table:formula="of:=[.A1]*2" office:value-type="float" office:value="8"><text:p>8</text:p></table:table-cell>
                  </table:table-row>
                </table:table>
              </office:spreadsheet></office:body>
            </office:document>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(flat));
        var reader = new OdsSpreadsheetReader();

        Assert.True(reader.CanRead(stream));
        var workbook = Read(stream);

        var sheet = workbook.Sheets[0];
        Assert.Equal("Dati", sheet.Name);
        Assert.Equal(CellValue.Number(4), sheet.GetValue(A("A1")));
        Assert.True(workbook.Styles.Get(sheet.StyleIdAt(A("A1"))).Font.Bold);
        Assert.Equal("=A1*2", sheet[A("B1")].Formula);
    }

    [Fact]
    public void UnFileXmlCheNonEUnFoglioNonSiDichiaraLeggibile()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """<office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" office:mimetype="application/vnd.oasis.opendocument.text"/>"""));

        Assert.False(new OdsSpreadsheetReader().CanRead(stream));
    }

    [Fact]
    public void DataEOraConservanoIFrazioniDiSecondo()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Foglio1");
        var moment = 41299.54686342592;
        sheet.SetValue(A("A1"), CellValue.Number(moment));
        sheet.SetStyle(A("A1"), workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = 22 }));

        var value = RoundTrip(workbook).Sheets[0].GetValue(A("A1"));

        Assert.Equal(moment, value.AsNumber(), 9);
    }

    [Theory]
    [InlineData("8.7pt", 8.7)]
    [InlineData("0.5cm", 14.15)]
    [InlineData("12px", 9)]
    public void IlCorpoDelCarattereSiLeggeInOgniUnita(string size, double expected)
    {
        var content = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
              xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
              xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
              xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
              xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
              office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
              <office:automatic-styles>
                <style:style style:name="ce1" style:family="table-cell"><style:text-properties fo:font-size="{{size}}"/></style:style>
              </office:automatic-styles>
              <office:body><office:spreadsheet><table:table table:name="T"><table:table-row>
                <table:table-cell table:style-name="ce1" office:value-type="string"><text:p>x</text:p></table:table-cell>
              </table:table-row></table:table></office:spreadsheet></office:body>
            </office:document>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content.TrimStart()));
        var workbook = Read(stream);

        Assert.Equal(expected, workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(A("A1"))).Font.Size, 1);
    }
}

public class OdsControlCharacterTests
{
    [Fact]
    public void UnCarattereDiControlloNonImpedisceIlSalvataggio()
    {
        var workbook = new Workbook();
        workbook.AddSheet("A").SetValue(new CellAddress(0, 0), CellValue.Text("x\u0016y"));

        using var stream = new MemoryStream();
        Assert.True(new OdsSpreadsheetWriter().Write(workbook, stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(CellValue.Text("xy"), new OdsSpreadsheetReader().Read(stream).Value!.Sheets[0].GetValue(new CellAddress(0, 0)));
    }
}
