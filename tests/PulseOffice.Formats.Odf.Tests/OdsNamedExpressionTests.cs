using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsNamedExpressionTests
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

    [Fact]
    public void UnIntervalloNominatoTornaIndietro()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        workbook.DefineName(new DefinedName("Vendite", "A1:B10"));

        var riletto = RoundTrip(workbook);

        var nome = Assert.Single(riletto.DefinedNames);

        Assert.Equal("Vendite", nome.Name);
        Assert.Equal("Dati.A1:B10", nome.Expression);
    }

    [Fact]
    public void UnaCostanteNominataTornaIndietro()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        workbook.DefineName(new DefinedName("Aliquota", "0,22"));

        var nome = Assert.Single(RoundTrip(workbook).DefinedNames);

        Assert.Equal("Aliquota", nome.Name);
    }
}
