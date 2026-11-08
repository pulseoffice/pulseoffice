using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class StyleTransplantTests
{
    private readonly StyleTransplant _transplant = new();

    [Fact]
    public void Un_formato_personalizzato_segue_la_porzione_in_un_altra_cartella()
    {
        var source = new NumberFormatCatalog();
        var target = new NumberFormatCatalog();
        target.Register("0.000 \"kg\"");
        var id = source.Register("#,##0.00 \"€\"");
        var carried = _transplant.Carry(CellStyle.Default with { NumberFormatId = id }, source, target);
        Assert.Equal("#,##0.00 \"€\"", target.CodeOf(carried.NumberFormatId));
    }

    [Fact]
    public void I_formati_predefiniti_restano_quelli()
    {
        var style = CellStyle.Default with { NumberFormatId = 14 };
        Assert.Equal(14, _transplant.Carry(style, new NumberFormatCatalog(), new NumberFormatCatalog()).NumberFormatId);
    }

    [Fact]
    public void Un_codice_arrivato_dagli_appunti_si_registra_nella_cartella()
    {
        var target = new NumberFormatCatalog();
        var style = _transplant.WithFormat(CellStyle.Default, "0.0%", target);
        Assert.Equal("0.0%", target.CodeOf(style.NumberFormatId));
        Assert.Same(CellStyle.Default, _transplant.WithFormat(CellStyle.Default, null, target));
    }
}
