using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ListLegalNumberingRound99Tests
{
    private readonly IListLabeling _labeling = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IListLabeling>();

    [Fact]
    public void A_legal_level_shows_every_level_in_arabic_digits()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L",
        [
            new ListLevel(NumberFormat.UpperRoman),
            new ListLevel(NumberFormat.LowerLetter) { DisplayLevels = 2, Legal = true },
            new ListLevel(NumberFormat.LowerLetter) { DisplayLevels = 2 },
        ]));

        var labels = _labeling.Place(sheet, [Item(0), Item(0), Item(1), Item(2)]);

        Assert.Equal(["I.", "II.", "2.1.", "a.a."], labels.Select(label => label!.Label));
    }

    private static TextParagraph Item(int level) => new("x") { ListStyleName = "L", Numbered = true, BulletLevel = level };
}
