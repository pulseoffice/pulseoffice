using PulseOffice.Documents.Fields;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round301MergeTests
{
    private static readonly MergeData People = new(["Name", "City"], [["Anna", "Rome"], ["Bruno", "Milan"], ["Carla", "Turin"]]);

    [Fact]
    public void Excluded_records_follow_the_record_when_the_source_is_reordered()
    {
        var recipients = new MergeRecipients();
        var text = new MergeSelectionText();
        var saved = text.Read(text.Write(recipients.Keyed(People, new MergeSelection(MergeFilter.None, [], new HashSet<int> { 1 }))));
        var reordered = new MergeData(["Name", "City"], [["Dora", "Pisa"], ["Carla", "Turin"], ["Bruno", "Milan"], ["Anna", "Rome"]]);

        Assert.Equal([0, 1, 3], recipients.Rows(reordered, saved));
    }

    [Fact]
    public void Selections_without_record_keys_fall_back_to_record_numbers()
    {
        var recipients = new MergeRecipients();
        var saved = new MergeSelectionText().Read("{\"excluded\":[0,7]}");

        Assert.Equal([1, 2], recipients.Rows(People, saved));
    }

    [Fact]
    public void A_unique_key_column_keeps_the_exclusion_when_other_values_are_edited()
    {
        var recipients = new MergeRecipients();
        var text = new MergeSelectionText();
        var saved = text.Read(text.Write(recipients.Keyed(People, new MergeSelection(MergeFilter.None, [], new HashSet<int> { 1 }) { KeyColumn = "Name" })));
        var edited = new MergeData(["Name", "City"], [["Bruno", "Naples"], ["Anna", "Rome"], ["Carla", "Turin"]]);

        Assert.Equal("Name", saved.KeyColumn);
        Assert.Equal([1, 2], recipients.Rows(edited, saved));
    }

    [Fact]
    public void Only_columns_with_distinct_non_empty_values_are_offered_as_keys_and_others_fall_back_to_the_hash()
    {
        var recipients = new MergeRecipients();
        var data = new MergeData(["Id", "City"], [["1", "Rome"], ["2", "Rome"]]);

        Assert.Equal(["Id"], recipients.UniqueColumns(data));
        var keyed = recipients.Keyed(data, new MergeSelection(MergeFilter.None, [], new HashSet<int> { 0 }) { KeyColumn = "City" });
        Assert.Equal(string.Empty, keyed.KeyColumn);
        Assert.Equal([1], recipients.Rows(data, keyed));
    }
}
