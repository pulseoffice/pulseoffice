using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class DocumentPropertiesRoundTripTests
{
    private static Workbook ConProprieta()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");

        workbook.Properties.Title = "Rendiconto 2026";
        workbook.Properties.Subject = "Vendite";
        workbook.Properties.Description = "Prima stesura";
        workbook.Properties.Author = "Alice";
        workbook.Properties.LastModifiedBy = "Ada";
        workbook.Properties.Keywords.Add("vendite");
        workbook.Properties.Keywords.Add("2026");
        workbook.Properties.Custom["Reparto"] = "Nord";
        workbook.Properties.Created = new DateTimeOffset(2026, 3, 18, 9, 30, 0, TimeSpan.Zero);

        return workbook;
    }

    [Fact]
    public void LeProprietaSopravvivonoAlGiro()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(ConProprieta(), stream);

        stream.Position = 0;
        var risultato = new OdsReader().Read(stream);

        Assert.True(risultato.Succeeded);

        var proprieta = risultato.Value!.Properties;

        Assert.Equal("Rendiconto 2026", proprieta.Title);
        Assert.Equal("Vendite", proprieta.Subject);
        Assert.Equal("Prima stesura", proprieta.Description);
        Assert.Equal("Alice", proprieta.Author);
        Assert.Equal("Ada", proprieta.LastModifiedBy);
        Assert.Equal(["vendite", "2026"], proprieta.Keywords);
        Assert.Equal("Nord", proprieta.Custom["Reparto"]);
        Assert.Equal(2026, proprieta.Created!.Value.Year);
    }
}
