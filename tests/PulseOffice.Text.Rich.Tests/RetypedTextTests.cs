using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class RetypedTextTests
{
    [Fact]
    public void UniformFormattingSurvivesTypingNewText()
    {
        var text = new RichText(PlaceholderRole.Title, [new TextParagraph(string.Empty) { Runs = [new TextRun(string.Empty) { Font = "Pacifico", Bold = true }] }]);

        var retyped = text.WithPlainText("Benvenuti");

        var run = Assert.Single(retyped.Paragraphs[0].Runs);
        Assert.Equal("Benvenuti", run.Text);
        Assert.Equal("Pacifico", run.Font);
        Assert.True(run.Bold);
    }

    [Fact]
    public void AFontChosenOnAnEmptyPlaceholderSurvivesTyping()
    {
        var empty = new RichText(PlaceholderRole.Title, [new TextParagraph(string.Empty) { FontSize = 40, Bold = true }]);

        var formatted = new RichTextFormatter().SetFont(empty, null, "Pacifico");
        var run = Assert.Single(formatted.WithPlainText("Benvenuti").Paragraphs[0].EffectiveRuns());

        Assert.Equal("Pacifico", run.Font);
        Assert.Equal("Benvenuti", run.Text);
    }

    [Fact]
    public void MixedFormattingFallsBackToTheParagraph()
    {
        var text = new RichText(PlaceholderRole.Title, [new TextParagraph("ab") { Runs = [new TextRun("a") { Bold = true }, new TextRun("b")] }]);

        Assert.Empty(text.WithPlainText("xy").Paragraphs[0].Runs);
    }
}
