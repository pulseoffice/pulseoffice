using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.DataDialogs;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round126SortOrderTests
{
    private readonly LocaleSortOrders _orders = new(new CultureSortOrders(), new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("en-US")));

    [Fact]
    public void A_remembered_custom_order_that_matches_no_offered_order_is_offered_too()
    {
        var offered = _orders.Current();

        var orders = _orders.Including([["High", "Medium", "Low"], offered[0].Items]);

        Assert.Equal(offered.Count + 1, orders.Count);
        Assert.Equal(["High", "Medium", "Low"], orders[^1].Items);
        Assert.Equal("High, Medium, Low", orders[^1].Label);
    }

    [Fact]
    public void A_remembered_order_is_matched_ignoring_case_and_offered_once()
    {
        var orders = _orders.Including([["a", "b"], ["A", "B"]]);

        Assert.Equal(_orders.Current().Count + 1, orders.Count);
    }
}
