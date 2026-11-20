using System.Globalization;
using PulseOffice.Documents.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round296MergeTests
{
    private static readonly MergeData People = new(["Name", "City", "Age"], [["Anna", "Rome", "40"], ["Bruno", "Milan", "9"], ["Carla", "Rome", "25"], ["Dario", "Turin", "70"]]);

    [Fact]
    public void Conditions_join_with_and_before_or()
    {
        var filter = new MergeFilter(
        [
            new MergeCondition("City", MergeFilterOperator.Equals, "rome"),
            new MergeCondition("Age", MergeFilterOperator.Greater, "30"),
            new MergeCondition("City", MergeFilterOperator.Equals, "Turin") { Join = MergeJoin.Or },
        ]);

        Assert.Equal([0, 3], new MergeRecipients().Rows(People, filter, new HashSet<int>()));
    }

    [Fact]
    public void Records_sort_numerically_and_by_several_keys()
    {
        var recipients = new MergeRecipients();

        Assert.Equal([1, 2, 0, 3], recipients.Rows(People, new MergeSelection(MergeFilter.None, [new MergeSort("Age", false)], new HashSet<int>())));
        Assert.Equal([2, 0, 3], recipients.Rows(People, new MergeSelection(MergeFilter.None, [new MergeSort("City", false), new MergeSort("Name", true)], new HashSet<int> { 1 })));
    }

    [Fact]
    public void Selection_text_round_trips()
    {
        var text = new MergeSelectionText();
        var selection = new MergeSelection(
            new MergeFilter([new MergeCondition("City", MergeFilterOperator.Contains, "o\"m"), new MergeCondition("Age", MergeFilterOperator.NotEmpty, string.Empty) { Join = MergeJoin.Or }]),
            [new MergeSort("Name", true)],
            new HashSet<int> { 3, 1 });

        Assert.Equal(selection, text.Read(text.Write(selection)));
        Assert.True(text.Read("not json").IsEmpty);
        Assert.True(MergeSelection.All.IsEmpty);
    }

    [Fact]
    public void Merge_values_take_a_date_picture()
    {
        var shown = new MergeValueFormat().Shown("2026-03-05", new FieldDetail { DatePicture = "d MMMM yyyy" }, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("5 March 2026", shown);
        Assert.Equal("abc", new MergeValueFormat().Shown("abc", new FieldDetail { DatePicture = "yyyy" }, CultureInfo.InvariantCulture));
    }
}
