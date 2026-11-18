using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round215PageStyleCopyTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void A_page_style_copy_carries_header_and_footer_text_with_fresh_block_ids()
    {
        var source = _f.Document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        source.Header.Enabled = true;
        source.Header.Default.Add(new ParagraphBlock("hp1", new TextParagraph("Head")));
        source.Footer.Enabled = true;
        source.Footer.Default.Add(new ParagraphBlock("fp1", new TextParagraph("Foot")));
        source.NextStyleName = DefaultStyleNames.Standard;
        var styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree), new PulseOffice.Documents.Clipboard.FragmentRemapper(_f.Stories, _f.Tree));

        var copy = styles.PageCopy(_f.Document, source, "Copied");

        Assert.Equal("Copied", copy.Name);
        Assert.True(copy.Header.Enabled);
        var header = Assert.IsType<ParagraphBlock>(Assert.Single(copy.Header.Default.Blocks));
        var footer = Assert.IsType<ParagraphBlock>(Assert.Single(copy.Footer.Default.Blocks));
        Assert.Equal("Head", header.Text);
        Assert.Equal("Foot", footer.Text);
        Assert.NotEqual("hp1", header.Id);
        Assert.NotEqual("fp1", footer.Id);
        Assert.Null(copy.NextStyleName);
        Assert.Equal("hp1", source.Header.Default.Blocks[0].Id);
    }
}
