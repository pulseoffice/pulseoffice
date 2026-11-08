using System.Globalization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class SystemDateInputTests
{
    [Theory]
    [InlineData("en-US", "10/18/2026", "m/d/yyyy")]
    [InlineData("it-IT", "18/10/2026", "dd/mm/yyyy")]
    [InlineData("de-DE", "18.10.2026", "dd.mm.yyyy")]
    public void UnaDataDigitataPrendeLaDataBreveDelleImpostazioniLocali(string culture, string typed, string expected)
    {
        var recognizer = new CellInputRecognizer(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo(culture)), new SystemDateCode());

        var input = recognizer.Recognize(typed);

        Assert.Equal(expected, input.FormatCode);
        Assert.Equal(46313, input.Value.AsNumber());
    }
}
