using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round308ListFollowTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Listed()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Add(new ListStyle("Spaced", [
            new ListLevel(NumberFormat.Arabic) { Follow = ListLabelFollow.Space },
            new ListLevel(NumberFormat.LowerLetter) { Follow = ListLabelFollow.Nothing },
            new ListLevel(NumberFormat.Bullet)]));
        document.Styles.Paragraph.Add(new ParagraphStyle("Item") { ParentName = DefaultStyleNames.Standard, ListStyleName = "Spaced" });
        document.Body.Replace([new ParagraphBlock("p", "x", "Item")]);
        return document;
    }

    private static void AssertFollows(TextDocument read)
    {
        var levels = read.Styles.List.Find("Spaced")!.Levels;
        Assert.Equal([ListLabelFollow.Space, ListLabelFollow.Nothing, ListLabelFollow.Tab], levels.Take(3).Select(level => level.Follow));
    }

    [Fact]
    public void What_follows_a_list_label_crosses_docx()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Listed(), stream).Succeeded);
        stream.Position = 0;
        AssertFollows(_composer.Reader(DocxFlavour.Document).Read(stream).Value!);
    }

    [Fact]
    public void What_follows_a_list_label_crosses_odt()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Listed(), stream).Succeeded);
        stream.Position = 0;
        AssertFollows(new OdtReader().Read(stream).Value!);
    }
}
