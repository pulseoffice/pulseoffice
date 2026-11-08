using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing.Tables;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class CellValueLimitsTests
{
    private readonly CellValueText _values = new();

    [Theory]
    [InlineData(99_999_999d)]
    [InlineData(-1e12)]
    [InlineData(double.NaN)]
    public void UnaDataFuoriIntervalloNonSollevaEccezioni(double value)
    {
        var text = _values.Format(value, new CellNumberFormat(CellValueKind.Date), CultureInfo.InvariantCulture);

        Assert.False(string.IsNullOrEmpty(text));
    }

    [Fact]
    public void UnSimboloDiValutaVuotoLettoDalFileNonSollevaEccezioni()
    {
        var recognised = _values.TryValue("12.5", new CellNumberFormat(CellValueKind.Currency) { Symbol = string.Empty }, CultureInfo.InvariantCulture, out var value);

        Assert.True(recognised);
        Assert.Equal(12.5, value);
    }
}
