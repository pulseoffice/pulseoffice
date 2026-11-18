using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round191OdtListNumberFormatTests
{
    [Theory]
    [InlineData(NumberFormat.DecimalZero)]
    [InlineData(NumberFormat.CardinalText)]
    [InlineData(NumberFormat.IdeographDigital)]
    [InlineData(NumberFormat.ThaiLetters)]
    [InlineData(NumberFormat.ArabicAbjad)]
    public void Word_number_formats_cross_odt(NumberFormat format)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Replace(new ListStyle("Wide", [new ListLevel(format)]));
        document.Body.Replace([new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph("Item") { Runs = [new PulseOffice.Text.Rich.TextRun("Item")], ListStyleName = "Wide", Numbered = true })]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);

        Assert.Equal(format, read.Value!.Styles.List.All.Single(list => list.Name == "Wide").Levels[0].Format);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODT183");
    }
}
