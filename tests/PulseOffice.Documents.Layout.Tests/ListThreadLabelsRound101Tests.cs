using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ListThreadLabelsRound101Tests
{
    private readonly IListLabeling _labeling = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IListLabeling>();

    [Fact]
    public void An_interrupted_list_resumes_its_own_count_after_another_list_of_the_same_style()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.Arabic)]));

        var labels = _labeling.Place(sheet, [Item(null), Item(null), Item("list1"), Item("list1"), Item(null), Item("list1")]);

        Assert.Equal(["1.", "2.", "1.", "2.", "3.", "3."], labels.Select(label => label!.Label));
    }

    private static TextParagraph Item(string? thread) => new("x") { ListStyleName = "L", Numbered = true, ListId = thread };
}
