using PulseOffice.Text.Redaction;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckTextRedactionTests
{
    private readonly TextRedactor _redactor = new();

    private DeckTextRedaction Redaction() => new(_redactor);

    [Fact]
    public void UnIndirizzoDivisoFraDuePorzioniVieneOscuratoLostesso()
    {
        var paragraph = new TextParagraph(string.Empty).WithRuns(
            [new TextRun("Scrivi a ada@"), new TextRun("esempio.org") { Bold = true }]);
        var text = new RichText(PlaceholderRole.Body, [paragraph]);

        var redacted = Redaction().Redact(text, _redactor.Patterns);

        Assert.Equal($"Scrivi a {_redactor.Mask}", redacted.PlainText);
        Assert.DoesNotContain("esempio", redacted.PlainText);
    }

    [Fact]
    public void UnParagrafoSenzaDatiResta()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("Nessun dato qui")]);
        var redacted = Redaction().Redact(text, _redactor.Patterns);
        Assert.Same(text.Paragraphs[0], redacted.Paragraphs[0]);
    }

    [Fact]
    public void OscurareTuttoNonLasciaTestoMaConservaIParagrafiVuoti()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("Riservato"), new TextParagraph(string.Empty)]);
        var masked = Redaction().MaskAll(text);
        Assert.Equal(_redactor.Mask, masked.Paragraphs[0].Text);
        Assert.Equal(string.Empty, masked.Paragraphs[1].Text);
    }

    [Fact]
    public void AncheLeNoteSiOscurano() =>
        Assert.Equal($"Chiamare {_redactor.Mask}", Redaction().RedactNotes("Chiamare +39 06 1234 5678", _redactor.Patterns));

    [Fact]
    public void UnCampoOscuratoNonSiRicalcolaPiu()
    {
        var paragraph = new TextParagraph(string.Empty).WithRuns(
            [new TextRun("ada@esempio.org") { Field = TextFieldKind.Author }]);
        var redacted = Redaction().Redact(new RichText(PlaceholderRole.Body, [paragraph]), _redactor.Patterns);
        Assert.Equal(TextFieldKind.None, redacted.Paragraphs[0].Runs[0].Field);
    }
}
