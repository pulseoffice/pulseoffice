using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round183OdtListSeparatorTests
{
    [Fact]
    public void A_level_separator_crosses_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Replace(new ListStyle("Dashed", [new ListLevel(NumberFormat.Arabic), new ListLevel(NumberFormat.Arabic) { DisplayLevels = 2, Separator = "-" }]));
        document.Body.Replace([new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph("Item") { Runs = [new PulseOffice.Text.Rich.TextRun("Item")], ListStyleName = "Dashed", Numbered = true, BulletLevel = 1 })]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);

        Assert.Equal("-", read.Value!.Styles.List.All.Single(list => list.Name == "Dashed").Levels[1].Separator);
    }
}
