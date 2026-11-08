using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class RoundSixtyTwoLineSpacingTests
{
    [Fact]
    public void ProportionalLineHeightIsBoundedInTheModel()
    {
        Assert.Equal(LineSpacingFactor.Maximum, ParagraphLineHeight.Proportional(1e300).Value);
        Assert.Equal(LineSpacingFactor.Maximum, (ParagraphLineHeight.Proportional(2) with { Value = 1e300 }).Value);
        Assert.Equal(1e300, ParagraphLineHeight.Exact(1e300).Value);
        Assert.Equal(LineSpacingFactor.Maximum, new TextParagraph("x") { LineSpacing = 1e300 }.LineSpacing);
        Assert.Equal(LineSpacingFactor.Maximum, new ParagraphProperties { LineSpacing = 1e300 }.LineSpacing);
    }

    [Fact]
    public void HugeProportionalFactorsGiveFiniteLines()
    {
        var f = new LayoutFixture(width: 300, height: 400);
        f.Add("uno", p => p with { LineHeight = ParagraphLineHeight.Proportional(1e300) });
        f.Add("due", p => p with { LineSpacing = 1e300, Explicit = ParagraphOverrides.LineSpacing });
        f.Add("tre");

        var layout = f.Layout();

        Assert.All(f.Lines(layout), line => Assert.True(double.IsFinite(line.Top) && double.IsFinite(line.Line.Height)));
    }
}
