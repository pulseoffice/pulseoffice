using PulseOffice.Sheets.Pivot;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round267XlsxPivotTests
{
    private static PivotField Amount(ValueDisplayMode mode) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PulseOffice.Sheets.Pivot.PivotArea.Value, DisplayMode = mode, BaseField = "Year" };

    private static PivotField Through(PivotField value) => Round264XlsxPivotTests.Through(Round264XlsxPivotTests.Pivoted(value));

    [Fact]
    public void Percent_of_parent_column_is_not_read_as_row_parent()
    {
        Assert.Equal(ValueDisplayMode.PercentOfParentColumn, Through(Amount(ValueDisplayMode.PercentOfParentColumn)).DisplayMode);
    }

    [Fact]
    public void A_named_or_next_base_item_crosses_xlsx()
    {
        Assert.Equal(PivotBaseItem.Named("2024"), Through(Amount(ValueDisplayMode.DifferenceFrom) with { BaseItem = PivotBaseItem.Named("2024") }).BaseItem);
        Assert.Equal(PivotBaseItem.Next, Through(Amount(ValueDisplayMode.DifferenceFrom) with { BaseItem = PivotBaseItem.Next }).BaseItem);
        Assert.Equal(PivotBaseItem.Previous, Through(Amount(ValueDisplayMode.DifferenceFrom)).BaseItem);
    }
}
