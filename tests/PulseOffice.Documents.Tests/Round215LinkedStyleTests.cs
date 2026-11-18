using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round215LinkedStyleTests
{
    private readonly Fixture _f = new();

    public Round215LinkedStyleTests()
    {
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Title Linked") { ParentName = DefaultStyleNames.Standard, LinkedStyleName = "Title Linked Char" });
        _f.Document.Styles.Character.Add(new CharacterStyle("Title Linked Char") { LinkedStyleName = "Title Linked" });
        _f.Type("hello world");
    }

    [Fact]
    public void A_linked_paragraph_style_on_part_of_a_paragraph_applies_its_character_style()
    {
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 5)), "Title Linked");

        Assert.NotEqual("Title Linked", _f.First.Content.StyleName);
        Assert.Equal("Title Linked Char", _f.First.Content.Runs[0].StyleName);
        Assert.Equal("hello", _f.First.Content.Runs[0].Text);
    }

    [Fact]
    public void A_linked_paragraph_style_on_the_whole_paragraph_or_at_the_caret_sets_the_paragraph_style()
    {
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.At(_f.At(0, 3)), "Title Linked");
        Assert.Equal("Title Linked", _f.First.Content.StyleName);

        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 11)), DefaultStyleNames.Standard);
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 11)), "Title Linked");
        Assert.Equal("Title Linked", _f.First.Content.StyleName);
    }

    [Fact]
    public void Renaming_and_deleting_a_linked_style_updates_its_partner()
    {
        var styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree), new PulseOffice.Documents.Clipboard.FragmentRemapper(_f.Stories, _f.Tree));

        styles.Rename(_f.Document, _f.History, StyleFamily.Character, "Title Linked Char", "Renamed Char");
        Assert.Equal("Renamed Char", _f.Document.Styles.Paragraph.Find("Title Linked")!.LinkedStyleName);

        styles.Delete(_f.Document, _f.History, StyleFamily.Paragraph, "Title Linked");
        Assert.Null(_f.Document.Styles.Character.Find("Renamed Char")!.LinkedStyleName);
    }
}
