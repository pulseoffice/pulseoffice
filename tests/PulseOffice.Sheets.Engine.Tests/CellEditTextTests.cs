using System.Globalization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class CellEditTextTests
{
    private sealed class FixedLocale(string name) : ISpreadsheetLocale
    {
        public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(name);
    }

    private CellEditText Texts(string culture) => new(new FixedLocale(culture), new NumberFormatParser());

    private CellInputRecognizer Inputs(string culture) => new(new FixedLocale(culture));

    [Theory]
    [InlineData("it-IT", "18/10/2026")]
    [InlineData("en-US", "10/18/2026")]
    [InlineData("de-DE", "18.10.2026")]
    public void UnaDataSiModificaNelFormatoDellaLinguaERestaUnaData(string culture, string expected)
    {
        var serial = (new DateTime(2026, 10, 18) - new DateTime(1899, 12, 30)).TotalDays;

        var text = Texts(culture).Number(serial, "dd mmmm yyyy");
        var back = Inputs(culture).Recognize(text);

        Assert.Equal(expected, text);
        Assert.Equal(serial, back.Value.AsNumber(), 6);
        Assert.NotNull(back.FormatCode);
    }

    [Fact]
    public void DataEOraEOraSolaSiModificanoComeInCalc()
    {
        var serial = (new DateTime(2026, 10, 18, 14, 30, 0) - new DateTime(1899, 12, 30)).TotalDays;

        var moment = Texts("it-IT").Number(serial, "dd/mm/yyyy hh:mm");
        var clock = Texts("it-IT").Number(0.75, "hh:mm");

        Assert.Equal("18/10/2026 14:30:00", moment);
        Assert.Equal("18:00:00", clock);
        Assert.Equal(serial, Inputs("it-IT").Recognize(moment).Value.AsNumber(), 6);
    }

    [Fact]
    public void UnNumeroComuneRestaUnNumero()
    {
        Assert.Equal("1234,5", Texts("it-IT").Number(1234.5, "#,##0.00"));
        Assert.Equal("46313", Texts("it-IT").Number(46313, "General"));
    }

    [Theory]
    [InlineData("it-IT", "VERO", "FALSO")]
    [InlineData("en-US", "TRUE", "FALSE")]
    [InlineData("de-DE", "WAHR", "FALSCH")]
    [InlineData("es-ES", "VERDADERO", "FALSO")]
    [InlineData("fr-FR", "VRAI", "FAUX")]
    public void UnBooleanoSiModificaNellaLinguaDellaCellaERestaUnBooleano(string culture, string yes, string no)
    {
        var shownTrue = Texts(culture).Value(PulseOffice.Sheets.Values.CellValue.Boolean(true), string.Empty);
        var shownFalse = Texts(culture).Value(PulseOffice.Sheets.Values.CellValue.Boolean(false), string.Empty);

        Assert.Equal(yes, shownTrue);
        Assert.Equal(no, shownFalse);
        Assert.True(Inputs(culture).Recognize(shownTrue).Value.AsBoolean());
        Assert.False(Inputs(culture).Recognize(shownFalse).Value.AsBoolean());
        Assert.Equal(PulseOffice.Sheets.Values.ValueKind.Boolean, Inputs(culture).Recognize(shownFalse).Value.Kind);
    }
}
