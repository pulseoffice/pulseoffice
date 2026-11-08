using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class RichTextTransformTests
{
    private readonly IRichTextFormatter _formatter = new RichTextFormatter();

    private RichText Text(string content) =>
        new(PlaceholderRole.Body, [new TextParagraph(content)]);

    [Fact]
    public void CaseHelpersAreShared()
    {
        Assert.Equal("CIAO", new PulseOffice.Text.TextCases()[PulseOffice.Text.TextCaseKind.Upper].Apply("ciao"));
        Assert.Equal("ciao", new PulseOffice.Text.TextCases()[PulseOffice.Text.TextCaseKind.Lower].Apply("CIAO"));
        Assert.Equal("Ciao mondo", new PulseOffice.Text.TextCases()[PulseOffice.Text.TextCaseKind.Sentence].Apply("ciao MONDO"));
    }

    [Fact]
    public void TransformAppliesToWholePlaceholder()
    {
        var result = _formatter.Transform(Text("ciao mondo"), null, new PulseOffice.Text.TextCases()[PulseOffice.Text.TextCaseKind.Upper].Apply);

        Assert.Equal("CIAO MONDO", result.PlainText);
    }

    [Fact]
    public void LineSpacingSetsParagraph()
    {
        var result = _formatter.SetLineSpacing(Text("riga"), null, 1.5);

        Assert.Equal(1.5, result.Paragraphs[0].LineSpacing, 3);
    }

    [Fact]
    public void LaMaiuscolaDiFraseNonRiparteAOgniCambioDiFormato()
    {
        var text = new RichText(PlaceholderRole.Body,
        [
            new TextParagraph(string.Empty).WithRuns([new TextRun("ciao "), new TextRun("MONDO") { Bold = true }, new TextRun(" bel") ]),
            new TextParagraph(string.Empty).WithRuns([new TextRun("seconda RIGA")]),
        ]);

        var result = _formatter.Transform(text, null, new PulseOffice.Text.TextCases()[PulseOffice.Text.TextCaseKind.Sentence].Apply);

        Assert.Equal("Ciao mondo bel\nSeconda riga", result.PlainText);
        Assert.True(result.Paragraphs[0].Runs[1].Bold);
    }

    [Fact]
    public void LeIniziaiMaiuscoleNonSpezzanoUnaParolaInDueFormati()
    {
        var text = new RichText(PlaceholderRole.Body,
            [new TextParagraph(string.Empty).WithRuns([new TextRun("pa"), new TextRun("rola") { Italic = true }])]);

        var result = _formatter.Transform(text, (0, 6), new PulseOffice.Text.TextCases()[PulseOffice.Text.TextCaseKind.Title].Apply);

        Assert.Equal("Parola", result.PlainText);
    }
}
