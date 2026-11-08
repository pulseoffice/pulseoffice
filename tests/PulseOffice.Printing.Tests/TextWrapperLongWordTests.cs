using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class TextWrapperLongWordTests
{
    private static Twip Measure(string text) => new(text.Length * 10);

    [Fact]
    public void UnaParolaLungaInMezzoSiSpezza()
    {
        var lines = new TextWrapper().Wrap("abcdefghij xy", new Twip(40), Measure);

        Assert.All(lines, line => Assert.True(line.Length <= 4));
        Assert.Equal("abcdefghijxy", string.Concat(lines));
    }

    [Fact]
    public void UnaCoppiaSurrogataNonSiSpezza()
    {
        var lines = new TextWrapper().Wrap("a😀b", new Twip(10), Measure);

        Assert.DoesNotContain(lines, line => line.Length > 0 && char.IsHighSurrogate(line[^1]));
        Assert.DoesNotContain(lines, line => line.Length > 0 && char.IsLowSurrogate(line[0]));
    }
}
