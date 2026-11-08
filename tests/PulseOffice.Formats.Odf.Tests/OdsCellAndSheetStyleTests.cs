using System.IO.Compression;
using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsCellAndSheetStyleTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static CellStyle StyleRoundTrip(CellStyle style)
    {
        var source = Workbook.CreateDefault();
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        source.Sheets[0].SetValue(a1, CellValue.Number(1));
        source.Sheets[0].SetStyle(a1, source.Styles.Intern(style));
        var read = RoundTrip(source);
        return read.Styles.Get(read.Sheets[0][a1].StyleId);
    }

    [Fact]
    public void RotazioneEProtezioneDellaCellaSopravvivono()
    {
        var result = StyleRoundTrip(new CellStyle { RotationDegrees = -45, Locked = false });
        Assert.Equal(-45, result.RotationDegrees);
        Assert.False(result.Locked);
    }

    [Fact]
    public void IlRientroSopravvive()
    {
        Assert.Equal(3, StyleRoundTrip(new CellStyle { Indent = 3, Horizontal = CellHorizontalAlignment.Left }).Indent);
    }

    [Fact]
    public void AllineatoASinistraRestaDistintoDaGenerale()
    {
        Assert.Equal(CellHorizontalAlignment.Left, StyleRoundTrip(new CellStyle { Horizontal = CellHorizontalAlignment.Left }).Horizontal);
        Assert.Equal(CellHorizontalAlignment.General, StyleRoundTrip(new CellStyle()).Horizontal);
    }

    [Fact]
    public void FoglioNascostoColoreDellaSchedaEVersoSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var second = source.AddSheet("Nascosto");
        second.IsHidden = true;
        second.TabColorHex = "#FF0000";
        second.RightToLeft = true;

        var read = RoundTrip(source);
        Assert.False(read.Sheets[0].IsHidden);
        Assert.True(read.Sheets[1].IsHidden);
        Assert.Equal("#FF0000", read.Sheets[1].TabColorHex);
        Assert.True(read.Sheets[1].RightToLeft);
    }

    [Fact]
    public void GliInterruttoriDelCarattereLeggonoNoneEPesiNumerici()
    {
        const string content = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
              xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
              xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
              xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
              xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.3">
              <office:automatic-styles>
                <style:style style:name="ce1" style:family="table-cell">
                  <style:text-properties fo:font-weight="700" fo:font-style="oblique"
                    style:text-underline-style="none" style:text-line-through-style="none" style:text-overline-style="none"/>
                </style:style>
                <style:style style:name="ce2" style:family="table-cell">
                  <style:text-properties fo:font-weight="400" style:text-underline-style="solid"/>
                </style:style>
              </office:automatic-styles>
              <office:body><office:spreadsheet><table:table table:name="F">
                <table:table-row>
                  <table:table-cell table:style-name="ce1" office:value-type="float" office:value="1"/>
                  <table:table-cell table:style-name="ce2" office:value-type="float" office:value="2"/>
                </table:table-row>
              </table:table></office:spreadsheet></office:body>
            </office:document-content>
            """;

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("mimetype", CompressionLevel.NoCompression).Open()))
                writer.Write("application/vnd.oasis.opendocument.spreadsheet");
            using (var writer = new StreamWriter(archive.CreateEntry("content.xml").Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var read = result.Value!;

        var first = read.Styles.Get(read.Sheets[0][new CellAddress(0, 0)].StyleId).Font;
        Assert.True(first.Bold);
        Assert.True(first.Italic);
        Assert.False(first.Underline);
        Assert.False(first.Strikethrough);
        Assert.False(first.Overline);

        var second = read.Styles.Get(read.Sheets[0][new CellAddress(0, 1)].StyleId).Font;
        Assert.False(second.Bold);
        Assert.True(second.Underline);
    }

    [Theory]
    [InlineData("https://example.org/a?b=1")]
    [InlineData("#Dati 2024.B3")]
    [InlineData("#Sheet1.A1")]
    [InlineData("altri/bilancio.ods")]
    public void ICollegamentiSuCelleNumericheInterniERelativiSopravvivono(string link)
    {
        var source = Workbook.CreateDefault();
        source.AddSheet("Dati 2024");
        Assert.True(CellAddress.TryParseA1("B2", out var b2));
        source.Sheets[0][b2] = new Cell { Value = CellValue.Number(42), Hyperlink = link };

        var read = RoundTrip(source).Sheets[0][b2];
        Assert.Equal(link, read.Hyperlink);
        Assert.Equal(42, read.Value.AsNumber());
    }
}
