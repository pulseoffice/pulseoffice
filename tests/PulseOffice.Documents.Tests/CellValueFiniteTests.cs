using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing.Tables;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class CellValueFiniteTests
{
    private readonly CellValueText _values = new();

    [Theory]
    [InlineData("NaN", CellValueKind.Number)]
    [InlineData("Infinity", CellValueKind.Percent)]
    [InlineData("-Infinity", CellValueKind.Currency)]
    [InlineData("NaN", CellValueKind.Date)]
    public void TextThatParsesToANonFiniteNumberIsNotAValue(string text, CellValueKind kind)
    {
        Assert.False(_values.TryValue(text, new CellNumberFormat(kind), CultureInfo.InvariantCulture, out _));
    }
}
