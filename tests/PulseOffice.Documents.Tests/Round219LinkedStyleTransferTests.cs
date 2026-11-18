using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round219LinkedStyleTransferTests
{
    private readonly Fixture _f = new();

    private StyleSheet Source()
    {
        var sheet = new StyleSheet();
        sheet.Paragraph.Add(new ParagraphStyle("Lead") { LinkedStyleName = "Lead Char" });
        sheet.Character.Add(new CharacterStyle("Lead Char") { LinkedStyleName = "Lead" });
        sheet.Paragraph.Add(new ParagraphStyle("Orphan") { LinkedStyleName = "Gone Char" });
        return sheet;
    }

    [Fact]
    public void Copying_a_linked_paragraph_style_brings_its_partner()
    {
        var references = new FragmentReferences();
        references.ParagraphStyles.Add("Lead");

        var collected = new StyleClosure(new FragmentScanner(_f.Tree)).Collect(Source(), references);

        Assert.Equal("Lead", collected.Character.Find("Lead Char")!.LinkedStyleName);
    }

    [Fact]
    public void Merging_drops_a_link_to_a_missing_partner()
    {
        var recorder = new EditRecorder(_f.Document, "paste");

        new StyleMerger().Merge(recorder, _f.Document.Styles, Source());

        Assert.Null(_f.Document.Styles.Paragraph.Find("Orphan")!.LinkedStyleName);
        Assert.Equal("Lead Char", _f.Document.Styles.Paragraph.Find("Lead")!.LinkedStyleName);
    }

    [Fact]
    public void Loading_styles_keeps_complete_links_and_drops_dangling_ones()
    {
        var styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree), new FragmentRemapper(_f.Stories, _f.Tree));

        styles.Load(_f.Document, _f.History, Source(), overwrite: false);

        Assert.Equal("Lead", _f.Document.Styles.Character.Find("Lead Char")!.LinkedStyleName);
        Assert.Null(_f.Document.Styles.Paragraph.Find("Orphan")!.LinkedStyleName);
    }
}
