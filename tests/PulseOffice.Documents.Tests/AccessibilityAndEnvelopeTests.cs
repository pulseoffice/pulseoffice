using PulseOffice.Documents.Accessibility;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class AccessibilityAndEnvelopeTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void IlControlloTrovaIProblemiTipici()
    {
        _f.Document.Body.Replace([
            new ParagraphBlock(_f.Document.NextId("p"), "Capitolo", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock(_f.Document.NextId("p"), "Sotto", DefaultStyleNames.HeadingLevel(3)),
            new ParagraphBlock(_f.Document.NextId("p"), ""),
            new ParagraphBlock(_f.Document.NextId("p"), ""),
            new ParagraphBlock(_f.Document.NextId("p"), new TextParagraph("chiaro") { Runs = [new TextRun("chiaro") { Color = 0xFFEEEEEE }] }),
        ]);
        var audit = new AccessibilityAudit(
        [
            new DocumentMetadataRule(), new AlternativeTextRule(), new HeadingOrderRule(_f.Outline), new SpacingParagraphsRule(_f.Tree),
            new TableHeaderRule(_f.Tree), new ContrastRule(_f.Tree), new LinkTextRule(_f.Tree),
        ]);

        var kinds = audit.Check(_f.Document).Select(issue => issue.Kind).ToList();

        Assert.Contains(AccessibilityIssueKind.MissingTitle, kinds);
        Assert.Contains(AccessibilityIssueKind.SkippedHeadingLevel, kinds);
        Assert.Contains(AccessibilityIssueKind.EmptyParagraphs, kinds);
        Assert.Contains(AccessibilityIssueKind.LowContrast, kinds);
        Assert.DoesNotContain(AccessibilityIssueKind.TableWithoutHeader, kinds);
    }

    [Fact]
    public void LaBustaDiventaLaPrimaPaginaESiAnnulla()
    {
        _f.Type("Lettera");
        var envelopes = new Envelopes(new StyleEditing(new StyleReferences(_f.Stories, _f.Tree)));

        envelopes.Insert(_f.Document, _f.History, new EnvelopeRequest("Mario Rossi\nVia Roma 1", "Ditta", envelopes.Formats[0]));

        var paragraphs = _f.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal("Envelope", paragraphs[0].Content.PageStyleName);
        Assert.Equal(["Ditta", "Mario Rossi", "Via Roma 1", "Lettera"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal(DefaultStyleNames.Standard, paragraphs[3].Content.PageStyleName);
        Assert.Equal(Twip.FromMillimeters(220), _f.Document.Styles.Page.Find("Envelope")!.Width);

        _f.History.Undo();

        Assert.Equal(["Lettera"], _f.Document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.Null(_f.Document.Styles.Page.Find("Envelope"));
    }
}
