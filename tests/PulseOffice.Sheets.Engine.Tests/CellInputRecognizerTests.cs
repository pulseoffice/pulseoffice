using System.Globalization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class CellInputRecognizerTests
{
    private readonly CellInputRecognizer _italian = new(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT")));
    private readonly CellInputRecognizer _american = new(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("en-US")));

    [Theory]
    [InlineData("31/12/2026", 46387, "dd/mm/yyyy")]
    [InlineData("1/2/2026", 46054, "dd/mm/yyyy")]
    [InlineData("2026-12-31", 46387, "yyyy-mm-dd")]
    [InlineData("12,5%", 0.125, "0.00%")]
    [InlineData("20%", 0.2, "0%")]
    [InlineData("1.234,50 €", 1234.5, "#,##0.00 \"€\"")]
    [InlineData("€ 99", 99, "#,##0.00 \"€\"")]
    [InlineData("14:30", 0.6041666666666666, "hh:mm")]
    [InlineData("1.500", 1500, "#,##0")]
    [InlineData("42", 42, null)]
    [InlineData("3,25", 3.25, null)]
    public void InItalianoDateValutePercentualiEOreDiventanoNumeriConIlLoroFormato(string typed, double number, string? format)
    {
        var input = _italian.Recognize(typed);
        Assert.Equal(ValueKind.Number, input.Value.Kind);
        Assert.Equal(number, input.Value.AsNumber(), 9);
        Assert.Equal(format, input.FormatCode);
    }

    [Theory]
    [InlineData("1/2/99999999999")]
    [InlineData("99999999999/1/2026")]
    [InlineData("1.99999999999.2026")]
    public void DateConCifreOltreLIntero32RestanoTestoSenzaEccezione(string typed)
    {
        var input = _italian.Recognize(typed);
        Assert.Equal(ValueKind.Text, input.Value.Kind);
    }

    [Fact]
    public void InAmericanoIlMeseVienePrima()
    {
        var input = _american.Recognize("12/31/2026");
        Assert.Equal(46387, input.Value.AsNumber());
        Assert.Equal("m/d/yyyy", input.FormatCode);
    }

    [Theory]
    [InlineData("VERO", true)]
    [InlineData("falso", false)]
    [InlineData("TRUE", true)]
    public void IValoriLogiciSiRiconosconoNellaLinguaENellaFormaCanonica(string typed, bool expected)
    {
        var input = _italian.Recognize(typed);
        Assert.Equal(ValueKind.Boolean, input.Value.Kind);
        Assert.Equal(expected, input.Value.AsBoolean());
    }

    [Theory]
    [InlineData("Marketing")]
    [InlineData("31/02/2026")]
    [InlineData("12/31/2026")]
    [InlineData("A1")]
    public void IlTestoResta(string typed) => Assert.Equal(ValueKind.Text, _italian.Recognize(typed).Value.Kind);
}
