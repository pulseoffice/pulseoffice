using System.Globalization;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class Round166PageOffsetTests
{
    private static readonly PageContext Context = new(4, 9, "a.ods", "S", DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData("&P+10", "14")]
    [InlineData("&P-1 of &N", "3 of 9")]
    [InlineData("&P+", "4+")]
    public void Page_arithmetic_is_applied_to_the_number(string pattern, string expected) =>
        Assert.Equal(expected, new HeaderFooterFields(CultureInfo.InvariantCulture).Expand(pattern, Context));
}
