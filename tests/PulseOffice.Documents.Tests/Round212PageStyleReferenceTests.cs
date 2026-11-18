using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round212PageStyleReferenceTests
{
    private readonly Fixture _f = new();
    private readonly StyleEditing _styles;

    public Round212PageStyleReferenceTests()
    {
        _styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree), new PulseOffice.Documents.Clipboard.FragmentRemapper(_f.Stories, _f.Tree));
    }

    [Fact]
    public void Renaming_a_page_style_follows_the_paragraph_styles_that_break_to_it_and_undo_restores_them()
    {
        var sheet = _f.Document.Styles;
        sheet.Page.Add(new PageStyle("Wide"));
        sheet.Paragraph.Add(new ParagraphStyle("Chapter") { Paragraph = ParagraphProperties.None with { PageStyleName = "Wide" } });

        _styles.Rename(_f.Document, _f.History, StyleFamily.Page, "Wide", "Broad");

        Assert.Equal("Broad", sheet.Paragraph.Find("Chapter")!.Paragraph.PageStyleName);
        _f.History.Undo();
        Assert.Equal("Wide", sheet.Paragraph.Find("Chapter")!.Paragraph.PageStyleName);
    }

    [Fact]
    public void Deleting_a_page_style_moves_the_paragraph_styles_that_break_to_it_to_the_standard_page()
    {
        var sheet = _f.Document.Styles;
        sheet.Page.Add(new PageStyle("Wide"));
        sheet.Paragraph.Add(new ParagraphStyle("Chapter") { Paragraph = ParagraphProperties.None with { PageStyleName = "Wide" } });

        _styles.Delete(_f.Document, _f.History, StyleFamily.Page, "Wide");

        Assert.Equal(DefaultStyleNames.Standard, sheet.Paragraph.Find("Chapter")!.Paragraph.PageStyleName);
    }
}
