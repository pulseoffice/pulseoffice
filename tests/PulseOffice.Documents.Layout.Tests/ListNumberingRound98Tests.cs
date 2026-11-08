using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ListNumberingRound98Tests
{
    private readonly IListLabeling _labeling = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IListLabeling>();

    private static TextParagraph Item(int level = 0, int? restart = null) =>
        new("x") { ListStyleName = "L", Numbered = true, BulletLevel = level, ListRestart = restart };

    private static StyleSheet Sheet(int start)
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L", [.. Enumerable.Range(0, ListStyle.LevelCount).Select(_ => new ListLevel(NumberFormat.Arabic) { StartAt = start, DisplayLevels = 2 })]));
        return sheet;
    }

    [Fact]
    public void A_list_starting_at_zero_counts_up()
    {
        var labels = _labeling.Place(Sheet(0), [Item(), Item(), Item()]);

        Assert.Equal(["0.", "1.", "2."], labels.Select(label => label!.Label));
    }

    [Fact]
    public void A_restart_to_zero_keeps_counting_from_zero()
    {
        var labels = _labeling.Place(Sheet(1), [Item(), Item(restart: 0), Item()]);

        Assert.Equal(["1.", "0.", "1."], labels.Select(label => label!.Label));
    }

    [Fact]
    public void A_parent_restarted_below_its_start_value_shows_the_restart_in_the_child_label()
    {
        var labels = _labeling.Place(Sheet(3), [Item(restart: 1), Item(1)]);

        Assert.Equal("1.3.", labels[1]!.Label);
    }

    [Fact]
    public void An_unstyled_list_item_with_an_absurd_level_stays_within_the_level_range()
    {
        var sheet = new StyleSheet();
        var labels = _labeling.Place(sheet, [new TextParagraph("x") { Bullet = true, BulletLevel = 1000 }]);

        Assert.True(labels[0]!.ExtraMargin <= ListStyle.LevelCount * 100);
    }
}
