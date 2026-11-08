using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class RedactionTests
{
    private const string Mask = "■■■■";

    private readonly SheetRedaction _redaction = new(new PulseOffice.Text.Redaction.TextRedactor());

    private static Worksheet With(params string[] values)
    {
        var sheet = new Worksheet("Dati");

        for (var row = 0; row < values.Length; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(values[row]));

        return sheet;
    }

    private string Redacted(string value)
    {
        var sheet = With(value);
        var hits = _redaction.PlanAutomatic(sheet, _redaction.Patterns);

        return hits.Count == 0 ? value : hits[0].After;
    }

    [Theory]
    [InlineData("ada@esempio.org")]
    [InlineData("IT60X0542811101000000123456")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("RSSMRA85T10A562S")]
    [InlineData("IT60 X054 2811 1010 0000 0123 456")]
    [InlineData("3782 822463 10005")]
    [InlineData("6011000990139424123")]
    public void UnDatoRiconosciutoSpariscePerIntero(string value)
    {
        Assert.Equal(Mask, Redacted(value));
    }

    [Fact]
    public void NonRestanoFrammentiDelDatoOriginale()
    {
        var redacted = Redacted("IT60X0542811101000000123456");

        Assert.DoesNotContain("56", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("IT60", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void IlTestoIntornoRestaLeggibile()
    {
        Assert.Equal($"Scrivi a {Mask} entro domani", Redacted("Scrivi a ada@esempio.org entro domani"));
    }

    [Fact]
    public void UnNumeroDiTelefonoSiRiconosce()
    {
        Assert.Equal(Mask, Redacted("+39 06 1234 5678"));
    }

    [Fact]
    public void CioCheNonEUnDatoSensibileRestaComEra()
    {
        var sheet = With("Fatturato 2026", "Totale");

        Assert.Empty(_redaction.PlanAutomatic(sheet, _redaction.Patterns));
    }

    [Fact]
    public void LOscuramentoDellaSelezioneCancellaAncheLeFormule()
    {
        var sheet = With("ciao");
        sheet.SetFormula(new CellAddress(1, 0), "=A1");

        var hits = _redaction.Plan(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)));

        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(Mask, hit.After));
        Assert.Equal("=A1", hits[1].Before);
    }
}
