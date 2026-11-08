using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideLayoutAliasTests
{
    [Theory]
    [InlineData("tx", SlideLayout.TitleAndContent)]
    [InlineData("obj", SlideLayout.TitleAndContent)]
    [InlineData("txAndObj", SlideLayout.TwoContent)]
    [InlineData("nonEsiste", SlideLayout.Blank)]
    public void ITipiOdfEDiPowerPointSiRiconoscono(string type, SlideLayout expected) =>
        Assert.Equal(expected, new SlideLayoutRegistry().FromOoxml(type));
}
