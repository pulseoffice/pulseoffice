using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round191ListGapLabelTests
{
    private readonly IListLabeling _labeling = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IListLabeling>();

    [Fact]
    public void Each_gap_keeps_its_own_separator()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L",
        [
            new ListLevel(NumberFormat.Arabic),
            new ListLevel(NumberFormat.Arabic) { DisplayLevels = 2 },
            new ListLevel(NumberFormat.Arabic) { DisplayLevels = 3, Separator = ".", Separators = [".", "-"], Suffix = string.Empty },
        ]));

        var labels = _labeling.Place(sheet, [Item(0), Item(1), Item(2)]);

        Assert.Equal("1.1-1", labels[2]!.Label);
    }

    [Fact]
    public void Word_number_formats_label_list_items()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.DecimalZero), new ListLevel(NumberFormat.RussianLower) { DisplayLevels = 2, Suffix = ")" }]));

        var labels = _labeling.Place(sheet, [Item(0), Item(1), Item(1)]);

        Assert.Equal(["01.", "01.а)", "01.б)"], labels.Select(label => label!.Label));
    }

    private static TextParagraph Item(int level) => new("x") { ListStyleName = "L", Numbered = true, BulletLevel = level };
}
