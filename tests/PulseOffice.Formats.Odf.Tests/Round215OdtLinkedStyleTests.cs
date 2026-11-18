using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round215OdtLinkedStyleTests
{
    [Fact]
    public void Linked_paragraph_and_character_styles_cross_odt()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Quote Linked") { ParentName = DefaultStyleNames.Standard, LinkedStyleName = "Quote Linked Char" });
        document.Styles.Character.Add(new CharacterStyle("Quote Linked Char") { LinkedStyleName = "Quote Linked" });

        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        Assert.Equal("Quote Linked Char", read.Styles.Paragraph.Find("Quote Linked")!.LinkedStyleName);
        Assert.Equal("Quote Linked", read.Styles.Character.Find("Quote Linked Char")!.LinkedStyleName);
    }
}
