using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class StyleDeletionRound99Tests
{
    private readonly Fixture _f = new();
    private readonly StyleEditing _styles;

    public StyleDeletionRound99Tests()
    {
        _styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree));
    }

    [Fact]
    public void Deleting_a_table_style_reassigns_its_tables_to_the_parent_in_one_undo_step()
    {
        _f.Document.Styles.Table.Add(new TableStyle("Base"));
        _f.Document.Styles.Table.Add(new TableStyle("Mine") { ParentName = "Base" });
        var table = new TableBlock("t", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c", "x")]))])], [Foundation.Units.Twip.FromCentimeters(3)]) { StyleName = "Mine" };
        _f.Document.Body.Add(table);

        _styles.Delete(_f.Document, _f.History, StyleFamily.Table, "Mine");

        Assert.Equal("Base", table.StyleName);
        _f.History.Undo();
        Assert.Equal("Mine", table.StyleName);
        Assert.True(_f.Document.Styles.Table.Contains("Mine"));
    }

    [Fact]
    public void A_style_cannot_be_given_itself_or_a_descendant_as_parent()
    {
        var styles = _f.Document.Styles.Paragraph;
        styles.Add(new ParagraphStyle("A"));
        styles.Add(new ParagraphStyle("B") { ParentName = "A" });
        styles.Add(new ParagraphStyle("C") { ParentName = "B" });
        styles.Add(new ParagraphStyle("D"));

        var parents = new StyleLineage().PossibleParents(styles, "A");

        Assert.DoesNotContain("A", parents);
        Assert.DoesNotContain("B", parents);
        Assert.DoesNotContain("C", parents);
        Assert.Contains("D", parents);
    }

    [Fact]
    public void Deleting_a_character_style_clears_list_levels_and_drop_caps_that_use_it()
    {
        _f.Document.Styles.Character.Add(new CharacterStyle("Label"));
        _f.Document.Styles.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.Arabic) { CharacterStyleName = "Label" }]));
        _f.Type("Testo");
        var paragraph = _f.Paragraph(0);
        paragraph.Content = paragraph.Content with { DropCap = new DropCap(3, 1) { CharacterStyleName = "Label" } };

        _styles.Delete(_f.Document, _f.History, StyleFamily.Character, "Label");

        Assert.Null(_f.Document.Styles.List.Find("L")!.Levels[0].CharacterStyleName);
        Assert.Null(paragraph.Content.DropCap!.CharacterStyleName);
        _f.History.Undo();
        Assert.Equal("Label", _f.Document.Styles.List.Find("L")!.Levels[0].CharacterStyleName);
        Assert.Equal("Label", paragraph.Content.DropCap!.CharacterStyleName);
    }
}
