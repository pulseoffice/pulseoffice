using System.Text;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class FlatOdfTests
{
    private static readonly CellAddress A1 = new(0, 0);

    [Fact]
    public void UnFoglioPiattoScrittoDaNoiSiRilegge()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Conti");
        sheet.SetValue(A1, CellValue.Number(4));
        sheet.SetFormula(new CellAddress(0, 1), "=A1*2");

        using var stream = new MemoryStream();
        Assert.True(new FlatOdsSpreadsheetWriter().Write(workbook, stream).Succeeded);
        Assert.Contains("<office:document ", Encoding.UTF8.GetString(stream.ToArray()), StringComparison.Ordinal);
        stream.Position = 0;

        var reread = new OdsSpreadsheetReader().Read(stream).Value!;
        Assert.Equal(CellValue.Number(4), reread.Sheets[0].GetValue(A1));
        Assert.Equal("=A1*2", reread.Sheets[0][new CellAddress(0, 1)].Formula);
    }

    [Fact]
    public void UnaPresentazionePiattaSiScriveESiRilegge()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var deck = new Presentation();
        using var source = new MemoryStream();
        new OdpWriter().Write(deck, source);
        source.Position = 0;
        var original = new OdpReader(themes, masters).Read(source).Value!;

        using var flat = new MemoryStream();
        Assert.True(new FlatOdpWriter().Write(original, flat).Succeeded);
        flat.Position = 0;

        var reader = new OdpReader(themes, masters);
        Assert.True(reader.CanRead(flat));
        var reread = reader.Read(flat);
        Assert.True(reread.Succeeded, string.Join("; ", reread.Diagnostics));
        Assert.Equal(original.Slides.Count, reread.Value!.Slides.Count);
    }

    [Fact]
    public void IPrefissiDelleFunzioniDiCalcSiTolgonoInLetturaESiRimettonoInScrittura()
    {
        using var stream = new MemoryStream(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Calc", "tdf162177_Eastersunday.fods")));
        var workbook = new OdsSpreadsheetReader().Read(stream).Value!;

        Assert.Equal("=EASTERSUNDAY(2024)", workbook.Sheets[0][A1].Formula);

        var syntax = new OdfFormulaSyntax();
        Assert.Equal("of:=COM.MICROSOFT.UNIQUE([.B1:.B4])", syntax.ToOdf("=UNIQUE(B1:B4)"));
        Assert.Equal("=UNIQUE(B1:B4)", syntax.ToInternal("of:=COM.MICROSOFT.UNIQUE([.B1:.B4])"));
        Assert.Equal("=\"COM.MICROSOFT.UNIQUE(\"", syntax.ToInternal("of:=\"COM.MICROSOFT.UNIQUE(\""));
    }

    [Fact]
    public void UnaCellaRipetutaSuColonneConStiliPredefinitiDiversiPrendeLoStileDiOgniColonna()
    {
        const string flat = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
            <office:automatic-styles>
            <style:style style:name="giallo" style:family="table-cell"><style:table-cell-properties fo:background-color="#e8f2a1"/></style:style>
            <style:style style:name="blu" style:family="table-cell"><style:text-properties fo:color="#3465a4"/></style:style>
            </office:automatic-styles>
            <office:body><office:spreadsheet><table:table table:name="Foglio1">
            <table:table-column table:default-cell-style-name="giallo"/><table:table-column table:default-cell-style-name="blu"/>
            <table:table-row><table:table-cell table:number-columns-repeated="2" office:value-type="float" office:value="1"><text:p>1</text:p></table:table-cell></table:table-row>
            </table:table></office:spreadsheet></office:body></office:document>
            """;
        var workbook = new OdsSpreadsheetReader().Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(flat))).Value!;
        var sheet = workbook.Sheets[0];

        Assert.Equal("#E8F2A1", workbook.Styles.Get(sheet[new PulseOffice.Sheets.Addressing.CellAddress(0, 0)].StyleId).BackgroundHex?.ToUpperInvariant());
        var second = workbook.Styles.Get(sheet[new PulseOffice.Sheets.Addressing.CellAddress(0, 1)].StyleId);
        Assert.Null(second.BackgroundHex);
        Assert.Equal("#3465A4", second.Font.ColorHex.ToUpperInvariant());
    }
}
