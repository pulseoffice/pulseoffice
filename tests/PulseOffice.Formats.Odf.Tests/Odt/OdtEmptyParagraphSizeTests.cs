using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtEmptyParagraphSizeTests
{
    [Fact]
    public void Il_corpo_di_un_paragrafo_vuoto_attraversa_l_ODT()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns([new TextRun(string.Empty) { FontSize = 4 }])));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Testo"));

        var read = OdtRoundTripTests.RoundTrip(document);

        var empty = read.Body.Blocks.OfType<ParagraphBlock>().First();
        Assert.Equal(4, new StyleResolver().EffectiveRun(read.Styles, empty.Content, empty.Content.Runs.FirstOrDefault() ?? new TextRun(string.Empty)).FontSize);
    }
}
