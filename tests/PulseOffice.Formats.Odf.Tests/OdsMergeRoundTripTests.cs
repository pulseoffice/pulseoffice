using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsMergeRoundTripTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook Unito()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.SetValue(A("A1"), CellValue.Text("Rendiconto trimestrale"));
        sheet.Merges.Add(R("A1:C1"));

        sheet.SetValue(A("A2"), CellValue.Text("Zona"));
        sheet.SetValue(A("B2"), CellValue.Text("Mese"));
        sheet.SetValue(A("C2"), CellValue.Number(3));

        return workbook;
    }

    [Fact]
    public void LUnioneTornaIndietro()
    {
        var sheet = RoundTrip(Unito()).Sheets[0];

        var regione = Assert.Single(sheet.Merges.All);

        Assert.Equal(R("A1:C1"), regione);
        Assert.Equal("Rendiconto trimestrale", sheet[A("A1")].Value.ToString());
    }

    [Fact]
    public void LeCelleDopoUnUnioneRestanoNellaLoroColonna()
    {
        var sheet = RoundTrip(Unito()).Sheets[0];

        Assert.Equal("Zona", sheet[A("A2")].Value.ToString());
        Assert.Equal("Mese", sheet[A("B2")].Value.ToString());
        Assert.Equal(3d, sheet[A("C2")].Value.AsNumber());
    }

    [Fact]
    public void UnaCellaCopertaConservaIlProprioContenuto()
    {
        var workbook = Unito();
        workbook.Sheets[0].SetValue(A("B1"), CellValue.Text("nascosto"));

        var sheet = RoundTrip(workbook).Sheets[0];

        Assert.Equal("nascosto", sheet[A("B1")].Value.ToString());
        Assert.True(sheet.Merges.IsCovered(A("B1")));
    }
}

public class OdsHyperlinkRoundTripTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void UnCollegamentoSopravviveAlGiro()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet[A("A1")] = new Cell
        {
            Value = CellValue.Text("Documentazione"),
            Hyperlink = "https://example.org/guida",
        };

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded);

        var cell = result.Value!.Sheets[0][A("A1")];

        Assert.Equal("https://example.org/guida", cell.Hyperlink);
        Assert.Equal("Documentazione", cell.Value.ToString());
    }
}
