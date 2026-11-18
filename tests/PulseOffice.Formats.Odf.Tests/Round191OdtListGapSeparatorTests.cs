using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round191OdtListGapSeparatorTests
{
    [Fact]
    public void Separators_between_each_pair_cross_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Replace(new ListStyle("Mixed",
        [
            new ListLevel(NumberFormat.Arabic),
            new ListLevel(NumberFormat.Arabic),
            new ListLevel(NumberFormat.Arabic) { DisplayLevels = 3, Separators = [".", "-"] },
        ]));
        document.Body.Replace([new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph("Item") { Runs = [new PulseOffice.Text.Rich.TextRun("Item")], ListStyleName = "Mixed", Numbered = true, BulletLevel = 2 })]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);

        Assert.Equal([".", "-"], read.Value!.Styles.List.All.Single(list => list.Name == "Mixed").Levels[2].Separators);
    }
}
