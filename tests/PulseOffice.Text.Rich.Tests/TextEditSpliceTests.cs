using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public class TextEditSpliceTests
{
    private readonly ITextEditSplice _splice = new TextEditSplice();

    private RichText Mixed() => new(PlaceholderRole.Body,
    [
        new TextParagraph("Uno due tre")
        {
            Runs = [new TextRun("Uno ") { Bold = true }, new TextRun("due"), new TextRun(" tre") { Highlight = 0xFFFFFF00 }],
        },
    ]);

    [Fact]
    public void TypingInTheMiddleKeepsTheOtherRuns()
    {
        var edited = _splice.Apply(Mixed(), "Uno duex tre");
        var runs = edited.Paragraphs[0].Runs;

        Assert.Equal("Uno duex tre", edited.Paragraphs[0].Text);
        Assert.True(runs[0].Bold);
        Assert.Equal("due" + "x", runs[1].Text);
        Assert.Equal(0xFFFFFF00u, runs[2].Highlight);
    }

    [Fact]
    public void TypedTextTakesTheStyleOfTheCharacterBefore()
    {
        var edited = _splice.Apply(Mixed(), "UnoX due tre");
        Assert.Equal("UnoX ", edited.Paragraphs[0].Runs[0].Text);
        Assert.True(edited.Paragraphs[0].Runs[0].Bold);
    }

    [Fact]
    public void EnterSplitsAndKeepsFormattingAndParagraphShape()
    {
        var text = Mixed() with { Paragraphs = [Mixed().Paragraphs[0] with { Align = TextAlign.Center }] };
        var edited = _splice.Apply(text, "Uno \ndue tre");

        Assert.Equal(2, edited.Paragraphs.Count);
        Assert.True(edited.Paragraphs[0].Runs[0].Bold);
        Assert.Equal(TextAlign.Center, edited.Paragraphs[1].Align);
        Assert.Equal(0xFFFFFF00u, edited.Paragraphs[1].Runs[^1].Highlight);
    }

    [Fact]
    public void DeletingAParagraphBreakMergesWithBothStyles()
    {
        var text = new RichText(PlaceholderRole.Body,
        [
            new TextParagraph("A") { Runs = [new TextRun("A") { Italic = true }] },
            new TextParagraph("B") { Runs = [new TextRun("B") { Bold = true }] },
        ]);

        var edited = _splice.Apply(text, "AB");

        Assert.Single(edited.Paragraphs);
        Assert.True(edited.Paragraphs[0].Runs[0].Italic);
        Assert.True(edited.Paragraphs[0].Runs[1].Bold);
    }

    [Fact]
    public void AFieldSurvivesUntilItIsTouched()
    {
        var text = new RichText(PlaceholderRole.Free,
        [
            new TextParagraph("Pag 3") { Runs = [new TextRun("Pag "), new TextRun("3") { Field = TextFieldKind.SlideNumber }] },
        ]);

        Assert.Equal(TextFieldKind.SlideNumber, _splice.Apply(text, "Una Pag 3").Paragraphs[0].Runs[^1].Field);
        Assert.Equal(TextFieldKind.SlideNumber, _splice.Apply(text, "Pag 34").Paragraphs[0].Runs[1].Field);
        Assert.All(_splice.Apply(text, "Pag 4").Paragraphs[0].Runs, run => Assert.Equal(TextFieldKind.None, run.Field));
    }

    [Fact]
    public void PlainParagraphsStayWithoutRuns()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("ciao") { Bold = true }]);
        var edited = _splice.Apply(text, "ciao mondo");

        Assert.Empty(edited.Paragraphs[0].Runs);
        Assert.Equal("ciao mondo", edited.Paragraphs[0].Text);
    }

    [Fact]
    public void AnEmptyPlaceholderKeepsTheChosenFont()
    {
        var text = new RichText(PlaceholderRole.Title, [new TextParagraph(string.Empty) { Runs = [new TextRun(string.Empty) { Font = "Pacifico" }] }]);
        Assert.Equal("Pacifico", _splice.Apply(text, "Titolo").Paragraphs[0].Runs[0].Font);
    }

    [Fact]
    public void ACarriageReturnAloneSplitsTheParagraph()
    {
        var edited = _splice.Apply(Mixed(), "Uno\rdue tre");
        Assert.Equal(2, edited.Paragraphs.Count);
        Assert.DoesNotContain('\r', edited.Paragraphs[1].Text);
    }

    [Fact]
    public void PlainTextWithCarriageReturnsSplitsTheParagraphs()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("a")]).WithPlainText("a\rb");
        Assert.Equal(2, text.Paragraphs.Count);
    }

    [Fact]
    public void ALongPasteIsLinearAndKeepsOneRun()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("x") { Runs = [new TextRun("x") { Bold = true }] }]);
        var pasted = "x" + new string('a', 400_000);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var edited = _splice.Apply(text, pasted);
        watch.Stop();

        Assert.Single(edited.Paragraphs[0].Runs);
        Assert.Equal(pasted, edited.Paragraphs[0].Runs[0].Text);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }
}
