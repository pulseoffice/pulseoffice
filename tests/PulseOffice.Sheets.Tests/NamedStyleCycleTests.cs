using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class NamedStyleCycleTests
{
    [Fact]
    public void ParentCycleFromAFileDoesNotRecurseForever()
    {
        var workbook = new Workbook();
        workbook.NamedStyles.Define("A", CellStyle.Default with { BackgroundHex = "#111111" }, parent: "B");
        workbook.NamedStyles.Define("B", CellStyle.Default with { Font = CellFont.Default with { Bold = true } }, parent: "A");
        var style = workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("A")!);
        Assert.Equal("#111111", style.BackgroundHex);
        Assert.True(style.Font.Bold);
    }

    [Fact]
    public void ChildInheritsFromTheWholeChain()
    {
        var workbook = new Workbook();
        var style = workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Heading 1")!);
        Assert.Equal(CellHorizontalAlignment.Center, style.Horizontal);
        Assert.Equal(13, style.Font.Size);
    }
}

public sealed class CalculationSettingsLimitTests
{
    [Fact]
    public void IterationCountFromAFileIsCapped()
    {
        Assert.Equal(CalculationSettings.MaximumIterations, new CalculationSettings { MaxIterations = int.MaxValue }.MaxIterations);
        Assert.Equal(1, new CalculationSettings { MaxIterations = -5 }.MaxIterations);
    }
}

public sealed class ConditionalRuleIconCountTests
{
    [Fact]
    public void EmptyIconSetNameFallsBackToThree() =>
        Assert.Equal(3, new PulseOffice.Sheets.Formatting.ConditionalRule { Range = PulseOffice.Sheets.Addressing.CellRange.Single(PulseOffice.Sheets.Addressing.CellAddress.First), IconSetName = "" }.IconCount);
}
