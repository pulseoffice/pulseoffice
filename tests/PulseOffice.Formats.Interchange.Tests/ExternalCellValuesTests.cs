using System.Globalization;
using PulseOffice.Formats.Interchange;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public sealed class ExternalCellValuesTests
{
    private static ExternalCellValues In(string culture) =>
        new(new CsvNumberParser(), new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void INumeriSiLeggonoNellaLinguaDelFoglio()
    {
        var italian = In("it-IT");

        Assert.Equal(1.5, italian.Parse("1,5").AsNumber());
        Assert.Equal(1250.5, italian.Parse("1.250,5").AsNumber());
        Assert.Equal(1.5, italian.Parse("1.5").AsNumber());
        Assert.Equal(ValueKind.Text, italian.Parse("007").Kind);
        Assert.Equal(ValueKind.Text, italian.Parse("Nord").Kind);
        Assert.True(italian.Parse("  ").IsEmpty);
    }
}
