using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round212StyleLoadTests
{
    private readonly Fixture _f = new();
    private readonly StyleEditing _styles;

    public Round212StyleLoadTests()
    {
        _styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree), new FragmentRemapper(_f.Stories, _f.Tree));
    }

    [Fact]
    public void Loading_page_styles_from_a_template_gives_their_header_paragraphs_ids_unused_in_the_document()
    {
        _f.Type("Body");
        var bodyId = _f.Paragraph(0).Id;
        var template = new StyleSheet();
        var letter = new PageStyle("Letter");
        letter.Header.Enabled = true;
        letter.Header.Default.Add(new ParagraphBlock(bodyId, new TextParagraph("Header")));
        template.Page.Add(letter);

        _styles.Load(_f.Document, _f.History, template, overwrite: false);

        var header = Assert.IsType<ParagraphBlock>(Assert.Single(_f.Document.Styles.Page.Find("Letter")!.Header.Default.Blocks));
        Assert.Equal("Header", header.Content.Text);
        Assert.NotEqual(bodyId, header.Id);
        Assert.Equal(bodyId, letter.Header.Default.Blocks[0].Id);
    }
}
