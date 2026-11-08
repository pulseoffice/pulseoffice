using PulseOffice.Foundation.Colors;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ArgbColorTests
{
    [Theory]
    [InlineData("#1A2B3C", 0xFF1A2B3Cu)]
    [InlineData("1a2b3c", 0xFF1A2B3Cu)]
    public void LeggeSeiCifreConOSenzaCancelletto(string text, uint expected) =>
        Assert.Equal(expected, ArgbColor.FromRgbHex(text)?.Value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#11223344")]
    public void RifiutaCiòCheNonÈUnColore(string? text) => Assert.Null(ArgbColor.FromRgbHex(text));

    [Fact]
    public void LaFormaOdfVuoleIlCancelletto()
    {
        Assert.Null(ArgbColor.FromHashedRgbHex("1A2B3C"));
        Assert.Equal(0xFF1A2B3Cu, ArgbColor.FromHashedRgbHex("#1a2b3c")?.Value);
    }

    [Fact]
    public void ScriveSenzaIlCanaleAlfa()
    {
        var colour = new ArgbColor(0x801A2B3C);

        Assert.Equal("#1A2B3C", colour.ToHashedHex());
        Assert.Equal("#1a2b3c", colour.ToLowerHashedHex());
        Assert.Equal("1A2B3C", colour.ToRgbDigits());
    }

    [Fact]
    public void GliSpaziNonSonoCifre()
    {
        Assert.Null(ArgbColor.FromRgbHex(" 12345"));
        Assert.Null(ArgbColor.FromRgbHex("#12345 "));
    }
}
