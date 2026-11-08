using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxFormulaSyntaxAuditTests
{
    private readonly XlsxFormulaSyntax _syntax = new();

    [Theory]
    [InlineData("=SUM(Sheet1.A1:Altro.B2)", "SUM(Sheet1:Altro!A1:B2)")]
    [InlineData("=SUM('Primo foglio'.A1:Altro.B2)", "SUM('Primo foglio:Altro'!A1:B2)")]
    [InlineData("=SUM(C:C)", "SUM(C:C)")]
    [InlineData("=SUM(Dati.C:C)", "SUM(Dati!C:C)")]
    [InlineData("=SUM(Dati.2:2)", "SUM(Dati!2:2)")]
    [InlineData("=SUM({1;2;3|4;5;6})", "SUM({1,2,3;4,5,6})")]
    [InlineData("=IF(A1;\"a;b\";B1)", "IF(A1,\"a;b\",B1)")]
    [InlineData("=SUM(B2#)", "SUM(_xlfn.ANCHORARRAY(B2))")]
    [InlineData("=SUM(A1:A2~B1)", "SUM((A1:A2,B1))")]
    [InlineData("=SUM(Dati.A1~Altro.B2:B3~C4)", "SUM((Dati!A1,Altro!B2:B3,C4))")]
    [InlineData("=INDEX(A1:B2~C1:D2;1;1;2)", "INDEX((A1:B2,C1:D2),1,1,2)")]
    public void LaSintassiAttraversaOoxmlNeiDueVersi(string internalFormula, string openXml)
    {
        Assert.Equal(openXml, _syntax.ToOpenXml(internalFormula));
        Assert.Equal(internalFormula, _syntax.ToInternal(openXml));
    }

    [Theory]
    [InlineData("SUM(Primo:Terzo!A1)", "=SUM(Primo.A1:Terzo.A1)")]
    [InlineData("SUM('Primo foglio:Terzo'!$A$1)", "=SUM('Primo foglio'.$A$1:Terzo.$A$1)")]
    public void IlRiferimento3DAUnaSolaCellaSiLeggeDaOoxml(string openXml, string internalFormula) =>
        Assert.Equal(internalFormula, _syntax.ToInternal(openXml));

    [Fact]
    public void IRiferimentiAdAltriDocumentiSonoUnaPerditaDichiarata()
    {
        var workbook = Workbook.CreateDefault();
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        workbook.Sheets[0].SetFormula(a1, "=ROWS(['file:///tmp/dati.ods'#$Foglio.A1:.A3])");

        var report = new XlsxSpreadsheetWriter().CheckFidelity(workbook);

        Assert.Contains(report.Losses, loss => loss.Code == "XLSX051");
    }

    [Theory]
    [InlineData("TableStyleTest.xlsx", "B10", "=COUNTIF($B$2:$B$9;\">5\")")]
    [InlineData("TableStyleTest.xlsx", "C10", "=SUBTOTAL(101;$C$2:$C$9)")]
    [InlineData("Book1_custom.xlsx", "D11", "=SUBTOTAL(109;$D$2:$D$10)")]
    public void IRiferimentiStrutturatiDelleTabelleDiventanoIntervalli(string file, string cell, string formula)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Excel", file));
        var result = new XlsxSpreadsheetReader().Read(stream);
        Assert.True(result.Succeeded);
        Assert.True(CellAddress.TryParseA1(cell, out var address));

        Assert.Equal(formula, result.Value!.Sheets[0][address].Formula);
        Assert.NotEmpty(result.Value.DatabaseRanges.All);
    }

    [Fact]
    public void UnCollegamentoEsternoPortaIlTestoMostrato()
    {
        var workbook = Workbook.CreateDefault();
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        workbook.Sheets[0].SetValue(a1, PulseOffice.Sheets.Values.CellValue.Text("Lotto"));
        workbook.Sheets[0][a1] = workbook.Sheets[0][a1] with { Hyperlink = "https://example.org/lotto" };

        using var stream = new MemoryStream();
        new XlsxSpreadsheetWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(stream);
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());

        Assert.Contains("display=\"Lotto\"", reader.ReadToEnd(), StringComparison.Ordinal);
    }
}
