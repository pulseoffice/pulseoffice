using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class StyleRenameRound101Tests
{
    private readonly Fixture _f = new();
    private readonly StyleEditing _styles;

    public StyleRenameRound101Tests()
    {
        _styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree));
    }

    [Fact]
    public void Renaming_a_paragraph_style_moves_every_reference_in_one_undo_step()
    {
        var sheet = _f.Document.Styles;
        sheet.Paragraph.Add(new ParagraphStyle("Mine") { ParentName = DefaultStyleNames.Standard });
        sheet.Paragraph.Add(new ParagraphStyle("Child") { ParentName = "Mine", NextStyleName = "Mine" });
        sheet.Paragraph.Find("Mine")!.NextStyleName = "Mine";
        _f.Type("Testo");
        var body = _f.Paragraph(0);
        body.Content = body.Content with { StyleName = "Mine" };
        var page = sheet.Page.Find(_f.Document.InitialPageStyle)!;
        page.Header.Enabled = true;
        var header = new ParagraphBlock("h", new TextParagraph("Header") { StyleName = "Mine" });
        page.Header.Default.Add(header);
        var index = new SectionBlock("toc", "Contents", new BlockContainer([new ParagraphBlock("e", new TextParagraph("Entry") { StyleName = "Mine" })]))
        {
            Index = new IndexDefinition(IndexKind.Contents) { LevelStyles = IndexLevelStyles.Defaults.WithEntry(1, "Mine").WithHeading("Mine") },
        };
        _f.Document.Body.Add(index);
        _f.Document.Notes.FootnoteSettings = _f.Document.Notes.FootnoteSettings with { ParagraphStyleName = "Mine" };
        var before = _f.History.StatePoint;

        _styles.Rename(_f.Document, _f.History, StyleFamily.Paragraph, "Mine", "Ours");

        Assert.Equal("style-rename", _f.History.UndoDescription);
        Assert.False(sheet.Paragraph.Contains("Mine"));
        Assert.Equal("Ours", sheet.Paragraph.Find("Ours")!.NextStyleName);
        Assert.Equal(DefaultStyleNames.Standard, sheet.Paragraph.Find("Ours")!.ParentName);
        Assert.Equal("Ours", sheet.Paragraph.Find("Child")!.ParentName);
        Assert.Equal("Ours", sheet.Paragraph.Find("Child")!.NextStyleName);
        Assert.Equal("Ours", body.Content.StyleName);
        Assert.Equal("Ours", Header(sheet).Content.StyleName);
        Assert.Equal("Ours", index.Index!.LevelStyles.Entry(1));
        Assert.Equal("Ours", index.Index.LevelStyles.Heading);
        Assert.Equal("Ours", ((ParagraphBlock)index.Content[0]).Content.StyleName);
        Assert.Equal("Ours", _f.Document.Notes.FootnoteSettings.ParagraphStyleName);

        _f.History.Undo();
        Assert.Same(before, _f.History.StatePoint);
        Assert.True(sheet.Paragraph.Contains("Mine"));
        Assert.False(sheet.Paragraph.Contains("Ours"));
        Assert.Equal("Mine", sheet.Paragraph.Find("Child")!.ParentName);
        Assert.Equal("Mine", body.Content.StyleName);
        Assert.Equal("Mine", Header(sheet).Content.StyleName);
        Assert.Equal("Mine", index.Index!.LevelStyles.Entry(1));
        Assert.Equal("Mine", _f.Document.Notes.FootnoteSettings.ParagraphStyleName);

        _f.History.Redo();
        Assert.Equal("Ours", body.Content.StyleName);
        Assert.Equal("Ours", index.Index!.LevelStyles.Heading);
    }

    [Fact]
    public void Renaming_onto_an_existing_name_changes_nothing()
    {
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Mine"));
        var before = _f.History.StatePoint;

        _styles.Rename(_f.Document, _f.History, StyleFamily.Paragraph, "Mine", DefaultStyleNames.TextBody);

        Assert.True(_f.Document.Styles.Paragraph.Contains("Mine"));
        Assert.Same(before, _f.History.StatePoint);
    }

    [Fact]
    public void Renaming_a_character_style_moves_runs_list_levels_and_note_citations()
    {
        var sheet = _f.Document.Styles;
        sheet.Character.Add(new CharacterStyle("Label"));
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.Arabic) { CharacterStyleName = "Label" }]));
        _f.Document.Notes.EndnoteSettings = _f.Document.Notes.EndnoteSettings with { CitationStyleName = "Label" };
        _f.Type("Testo");
        var paragraph = _f.Paragraph(0);
        paragraph.Content = paragraph.Content.WithRuns([new TextRun("Testo") { StyleName = "Label" }]);

        _styles.Rename(_f.Document, _f.History, StyleFamily.Character, "Label", "Tag");

        Assert.Equal("Tag", paragraph.Content.Runs[0].StyleName);
        Assert.Equal("Tag", sheet.List.Find("L")!.Levels[0].CharacterStyleName);
        Assert.Equal("Tag", _f.Document.Notes.EndnoteSettings.CitationStyleName);
    }

    [Fact]
    public void Renaming_a_page_style_moves_paragraphs_next_styles_and_the_initial_page()
    {
        var sheet = _f.Document.Styles;
        sheet.Page.Add(new PageStyle("Cover") { NextStyleName = "Cover" });
        sheet.Page.Find(DefaultStyleNames.LeftPage)!.NextStyleName = "Cover";
        _f.Document.InitialPageStyle = "Cover";
        _f.Type("Testo");
        var paragraph = _f.Paragraph(0);
        paragraph.Content = paragraph.Content with { NextPageStyleName = "Cover" };

        _styles.Rename(_f.Document, _f.History, StyleFamily.Page, "Cover", "Front");

        Assert.Equal("Front", _f.Document.InitialPageStyle);
        Assert.Equal("Front", sheet.Page.Find("Front")!.NextStyleName);
        Assert.Equal("Front", sheet.Page.Find(DefaultStyleNames.LeftPage)!.NextStyleName);
        Assert.Equal("Front", paragraph.Content.NextPageStyleName);
        _f.History.Undo();
        Assert.Equal("Cover", _f.Document.InitialPageStyle);
    }

    [Fact]
    public void Renaming_a_list_style_moves_paragraphs_and_paragraph_styles()
    {
        var sheet = _f.Document.Styles;
        sheet.List.Add(new ListStyle("L", [new ListLevel(NumberFormat.Arabic)]));
        sheet.Paragraph.Add(new ParagraphStyle("Listed") { ListStyleName = "L" });
        _f.Type("Testo");
        var paragraph = _f.Paragraph(0);
        paragraph.Content = paragraph.Content with { ListStyleName = "L", Numbered = true };

        _styles.Rename(_f.Document, _f.History, StyleFamily.List, "L", "Steps");

        Assert.Equal("Steps", paragraph.Content.ListStyleName);
        Assert.Equal("Steps", sheet.Paragraph.Find("Listed")!.ListStyleName);
    }

    [Fact]
    public void Deleting_a_paragraph_style_moves_index_levels_and_note_settings_to_the_parent()
    {
        var sheet = _f.Document.Styles;
        sheet.Paragraph.Add(new ParagraphStyle("Mine") { ParentName = DefaultStyleNames.TextBody });
        var index = new SectionBlock("toc", "Contents", new BlockContainer([new ParagraphBlock("e", "Entry")]))
        {
            Index = new IndexDefinition(IndexKind.Contents) { LevelStyles = IndexLevelStyles.Defaults.WithEntry(2, "Mine") },
        };
        _f.Document.Body.Add(index);
        _f.Document.Notes.FootnoteSettings = _f.Document.Notes.FootnoteSettings with { ParagraphStyleName = "Mine" };
        _f.Document.Notes.EndnoteSettings = _f.Document.Notes.EndnoteSettings with { ParagraphStyleName = "Mine" };

        _styles.Delete(_f.Document, _f.History, StyleFamily.Paragraph, "Mine");

        Assert.Equal(DefaultStyleNames.TextBody, index.Index!.LevelStyles.Entry(2));
        Assert.Equal(DefaultStyleNames.TextBody, _f.Document.Notes.FootnoteSettings.ParagraphStyleName);
        Assert.Equal(DefaultStyleNames.TextBody, _f.Document.Notes.EndnoteSettings.ParagraphStyleName);
        _f.History.Undo();
        Assert.Equal("Mine", index.Index!.LevelStyles.Entry(2));
        Assert.Equal("Mine", _f.Document.Notes.FootnoteSettings.ParagraphStyleName);
    }

    [Fact]
    public void Deleting_a_page_style_moves_deferred_page_styles_and_next_styles_away()
    {
        var sheet = _f.Document.Styles;
        sheet.Page.Add(new PageStyle("Wide"));
        sheet.Page.Find(DefaultStyleNames.FirstPage)!.NextStyleName = "Wide";
        _f.Type("Testo");
        var paragraph = _f.Paragraph(0);
        paragraph.Content = paragraph.Content with { NextPageStyleName = "Wide" };

        _styles.Delete(_f.Document, _f.History, StyleFamily.Page, "Wide");

        Assert.Equal(DefaultStyleNames.Standard, paragraph.Content.NextPageStyleName);
        Assert.Null(sheet.Page.Find(DefaultStyleNames.FirstPage)!.NextStyleName);
    }

    private static ParagraphBlock Header(StyleSheet sheet) =>
        sheet.Page.All.SelectMany(page => page.Header.Default.Blocks).OfType<ParagraphBlock>().Single(paragraph => paragraph.Id == "h");

    [Fact]
    public void Pasted_list_threads_never_join_a_thread_already_in_the_document()
    {
        _f.Type("Testo");
        var existing = _f.Paragraph(0);
        existing.Content = existing.Content with { ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = "list1" };
        var copied = new ParagraphBlock("x", new TextParagraph("Copia") { ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = "list1" });
        var other = new ParagraphBlock("y", new TextParagraph("Copia") { ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = "list1" });
        var fragment = new PulseOffice.Documents.Clipboard.DocumentFragment(new BlockContainer([copied, other]), _f.Document.Styles.Clone(), [], [], []);

        var remapped = new PulseOffice.Documents.Clipboard.FragmentRemapper(_f.Stories, _f.Tree).Remap(_f.Document, fragment);

        var threads = remapped.Blocks.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Content.ListId).ToList();
        Assert.NotEqual("list1", threads[0]);
        Assert.Equal(threads[0], threads[1]);
    }
}
