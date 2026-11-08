using PulseOffice.Documents.Layout.Flow;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class LetterNumberLimitsTests
{
    [Fact]
    public void AHugeValueDoesNotBuildAHugeLabel()
    {
        var label = new LetterNumbers(false).Render(2_000_000_000);
        Assert.True(label.Length <= 30, $"label of {label.Length} characters");
    }

    [Fact]
    public void AListStyleWithMoreThanTenLevelsDoesNotBreakTheLayout()
    {
        var f = new LayoutFixture(width: 300);
        var levels = Enumerable.Range(0, 14).Select(_ => new PulseOffice.Documents.Styles.ListLevel(PulseOffice.Documents.Styles.NumberFormat.Arabic)).ToList();
        f.Document.Styles.List.Add(new PulseOffice.Documents.Styles.ListStyle("Deep", levels));
        f.Add("deep", p => p with { ListStyleName = "Deep", BulletLevel = 12, Numbered = true });

        Assert.NotEmpty(f.Lines(f.Layout()));
    }

    [Fact]
    public void OrdinaryValuesKeepTheRepeatedLetters()
    {
        var letters = new LetterNumbers(true);
        Assert.Equal("A", letters.Render(1));
        Assert.Equal("AA", letters.Render(27));
    }
}
