using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round305LinkedStyleSyncTests
{
    private readonly Fixture _f = new();

    public Round305LinkedStyleSyncTests()
    {
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Lead") { ParentName = DefaultStyleNames.Standard, LinkedStyleName = "Lead Char" });
        _f.Document.Styles.Character.Add(new CharacterStyle("Lead Char") { LinkedStyleName = "Lead" });
        _f.Type("hello");
    }

    [Fact]
    public void Changing_a_linked_paragraph_style_font_changes_its_character_partner_and_undo_restores_both()
    {
        var styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree), new PulseOffice.Documents.Clipboard.FragmentRemapper(_f.Stories, _f.Tree));

        styles.Change(_f.Document, _f.History, _f.Document.Styles.Paragraph, "Lead", style => style.Character = new CharacterProperties { Bold = true }, "style-edit");
        Assert.True(_f.Document.Styles.Character.Find("Lead Char")!.Character.Bold);

        _f.History.Undo();
        Assert.Null(_f.Document.Styles.Character.Find("Lead Char")!.Character.Bold);
    }

    [Fact]
    public void Updating_a_linked_paragraph_style_from_the_selection_updates_its_partner()
    {
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Lead");
        _f.Editor.SetCharacter(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 5)), new CharacterProperties { Italic = true });

        new StyleFromSelection(_f.Stories, _f.Text, new DirectFormatting()).UpdateParagraphStyle(_f.Document, _f.History, _f.Paragraph(0).Id);

        Assert.True(_f.Document.Styles.Character.Find("Lead Char")!.Character.Italic);
    }
}
