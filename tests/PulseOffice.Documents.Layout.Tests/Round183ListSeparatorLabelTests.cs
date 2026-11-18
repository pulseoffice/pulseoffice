using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round183ListSeparatorLabelTests
{
    private readonly IListLabeling _labeling = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IListLabeling>();

    [Fact]
    public void A_level_separator_joins_the_shown_levels()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.Arabic), new ListLevel(NumberFormat.Arabic) { DisplayLevels = 2, Separator = "-" }]));

        var labels = _labeling.Place(sheet, [Item(0), Item(1), Item(1)]);

        Assert.Equal(["1.", "1-1.", "1-2."], labels.Select(label => label!.Label));
    }

    private static TextParagraph Item(int level) => new("x") { ListStyleName = "L", Numbered = true, BulletLevel = level };
}
