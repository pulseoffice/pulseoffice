using System.Globalization;
using PulseOffice.Sheets.UI.DataDialogs;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round87FilterMenuTests
{
    private static FilterColumnFacts Facts(int count, int blanks = 0) =>
        new([.. Enumerable.Range(0, count).Select(index => new FilterValueCount("v" + index.ToString("D7", CultureInfo.InvariantCulture), 1))], blanks, false, []);

    [Fact]
    public void A_million_distinct_values_list_only_a_bounded_page()
    {
        var model = new FilterMenuModel(Facts(1_000_000), null);

        Assert.Equal(FilterMenuModel.ListedLimit, model.Listed.Count);
        Assert.Equal(1_000_000 - FilterMenuModel.ListedLimit, model.Unlisted);

        model.Search = "v000012";
        Assert.Equal(10, model.Listed.Count);
        Assert.Equal(0, model.Unlisted);
    }

    [Fact]
    public void Unticking_only_blanks_keeps_a_filter_that_hides_blank_rows()
    {
        var model = new FilterMenuModel(Facts(3, blanks: 2), null) { Blanks = false };

        Assert.Null(model.Allowed());
        Assert.False(new ColumnFilter(0) { AllowedValues = model.Allowed(), IncludeBlanks = model.Blanks }.IsEmpty);
    }
}
