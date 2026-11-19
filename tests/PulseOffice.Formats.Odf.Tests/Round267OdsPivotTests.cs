using PulseOffice.Sheets.Pivot;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round267OdsPivotTests
{
    private static PivotField Amount(ValueDisplayMode mode) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value, DisplayMode = mode, BaseField = "Year" };

    [Fact]
    public void A_named_or_next_base_item_crosses_ods()
    {
        Assert.Equal(PivotBaseItem.Named("2024"), Round264OdsPivotTests.Through(Amount(ValueDisplayMode.DifferenceFrom) with { BaseItem = PivotBaseItem.Named("2024") }).BaseItem);
        Assert.Equal(PivotBaseItem.Next, Round264OdsPivotTests.Through(Amount(ValueDisplayMode.PercentDifferenceFrom) with { BaseItem = PivotBaseItem.Next }).BaseItem);
    }

    [Fact]
    public void Percent_of_parent_column_crosses_ods()
    {
        Assert.Equal(ValueDisplayMode.PercentOfParentColumn, Round264OdsPivotTests.Through(Amount(ValueDisplayMode.PercentOfParentColumn)).DisplayMode);
    }
}
