using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round196ListLanguageLabelTests
{
    private readonly IListLabeling _labeling = new ServiceCollection().AddDocumentsModel().AddDocumentsLayout().BuildServiceProvider().GetRequiredService<IListLabeling>();

    [Fact]
    public void Spelled_numbers_follow_the_document_language()
    {
        var labels = _labeling.Place(Sheet(), [Item(null), Item(null)], "it-IT");

        Assert.Equal(["Primo.", "Secondo."], labels.Select(label => label!.Label));
    }

    [Fact]
    public void The_paragraph_language_wins_over_the_document_language()
    {
        var labels = _labeling.Place(Sheet(), [Item("fr-FR")], "de-DE");

        Assert.Equal("Premier.", labels[0]!.Label);
    }

    private static StyleSheet Sheet()
    {
        var sheet = new StyleSheet();
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.OrdinalText)]));
        return sheet;
    }

    private static TextParagraph Item(string? language) =>
        new("x") { Runs = [new TextRun("x") { Language = language }], ListStyleName = "L", Numbered = true };
}
