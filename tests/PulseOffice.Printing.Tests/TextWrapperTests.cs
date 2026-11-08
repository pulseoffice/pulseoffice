using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class TextWrapperTests
{
    private static Twip Measure(string text) => new(text.Length * 100);

    [Fact]
    public void UnaRigaLungaVaACapoSulleParole() =>
        Assert.Equal(["uno due", "tre"], new TextWrapper().Wrap("uno due tre", new Twip(800), Measure));

    [Fact]
    public void UnaParolaPiuLungaDellaRigaSiSpezza() =>
        Assert.Equal(["abcd", "ef"], new TextWrapper().Wrap("abcdef", new Twip(400), Measure));

    [Fact]
    public void IRitorniACapoWindowsNonLascianoCaratteriDiControllo() =>
        Assert.Equal(["a", "b"], new TextWrapper().Wrap("a\r\nb", new Twip(800), Measure));
}
