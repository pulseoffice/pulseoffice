using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public interface IStyleReferences
{
    void Redirect(TextDocument document, EditRecorder recorder, StyleFamily family, string name, string? target);

    void RewriteParagraphs(TextDocument document, EditRecorder recorder, Func<TextParagraph, bool> affected, Func<TextParagraph, TextParagraph> change);

    void RewriteStyles<TStyle>(EditRecorder recorder, StyleCollection<TStyle> styles, Func<TStyle, bool> affected, Action<TStyle> change) where TStyle : DocumentStyle;
}

public sealed class StyleReferences(IStoryCatalog stories, IBlockTree tree) : IStyleReferences
{
    public void Redirect(TextDocument document, EditRecorder recorder, StyleFamily family, string name, string? target)
    {
        var sheet = document.Styles;
        switch (family)
        {
            case StyleFamily.Paragraph:
                RewriteStyles(recorder, sheet.Paragraph, style => style.ParentName == name || style.NextStyleName == name, style =>
                {
                    if (style.ParentName == name) style.ParentName = target;
                    if (style.NextStyleName == name) style.NextStyleName = target;
                });
                RewriteParagraphs(document, recorder, content => content.StyleName == name, content => content with { StyleName = target });
                Indexes(document, recorder, name, target);
                Notes(document, recorder, settings => settings.ParagraphStyleName == name ? settings with { ParagraphStyleName = target } : settings);
                break;
            case StyleFamily.Character:
                RewriteStyles(recorder, sheet.Character, style => style.ParentName == name, style => style.ParentName = target);
                RewriteParagraphs(document, recorder, content => content.Runs.Any(run => run.StyleName == name),
                    content => content.WithRuns([.. content.Runs.Select(run => run.StyleName == name ? run with { StyleName = target } : run)]));
                RewriteParagraphs(document, recorder, content => content.DropCap?.CharacterStyleName == name,
                    content => content with { DropCap = content.DropCap! with { CharacterStyleName = target } });
                RewriteStyles(recorder, sheet.List, style => style.Levels.Any(level => level.CharacterStyleName == name),
                    style => style.Levels = Levels(style.Levels, name, target));
                if (sheet.Outline.Levels.Any(level => level.CharacterStyleName == name))
                {
                    recorder.Touch<ListStyle>(sheet, () => [sheet.Outline], outlines => sheet.Outline = outlines[0]);
                    var outline = (ListStyle)sheet.Outline.Clone();
                    outline.Levels = Levels(outline.Levels, name, target);
                    sheet.Outline = outline;
                }
                Notes(document, recorder, settings => settings.CitationStyleName == name ? settings with { CitationStyleName = target } : settings);
                break;
            case StyleFamily.List:
                RewriteStyles(recorder, sheet.List, style => style.ParentName == name, style => style.ParentName = target);
                RewriteStyles(recorder, sheet.Paragraph, style => style.ListStyleName == name, style => style.ListStyleName = target);
                RewriteParagraphs(document, recorder, content => content.ListStyleName == name, content => content with { ListStyleName = target });
                break;
            case StyleFamily.Page:
                RewriteStyles(recorder, sheet.Page, style => style.ParentName == name || style.NextStyleName == name, style =>
                {
                    if (style.ParentName == name) style.ParentName = target;
                    if (style.NextStyleName == name) style.NextStyleName = target;
                });
                RewriteParagraphs(document, recorder, content => content.PageStyleName == name || content.NextPageStyleName == name, content => content with
                {
                    PageStyleName = content.PageStyleName == name ? target : content.PageStyleName,
                    NextPageStyleName = content.NextPageStyleName == name ? target : content.NextPageStyleName,
                });
                if (document.InitialPageStyle == name && target is not null)
                {
                    recorder.Touch<string>(document, () => [document.InitialPageStyle], names => document.InitialPageStyle = names[0]);
                    document.InitialPageStyle = target;
                }
                break;
            case StyleFamily.Frame:
                RewriteStyles(recorder, sheet.Frame, style => style.ParentName == name, style => style.ParentName = target);
                foreach (var item in document.Objects.All.Where(item => item.StyleName == name).ToList())
                {
                    recorder.Touch(item);
                    item.StyleName = target;
                }
                break;
            case StyleFamily.Table:
                RewriteStyles(recorder, sheet.Table, style => style.ParentName == name, style => style.ParentName = target);
                foreach (var block in stories.Stories(document).SelectMany(tree.Blocks).OfType<TableBlock>().Where(block => block.StyleName == name).ToList())
                {
                    recorder.Touch(block);
                    block.StyleName = target;
                }
                break;
        }
    }

    public void RewriteParagraphs(TextDocument document, EditRecorder recorder, Func<TextParagraph, bool> affected, Func<TextParagraph, TextParagraph> change)
    {
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => affected(paragraph.Content)).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.Content = change(paragraph.Content);
        }
    }

    public void RewriteStyles<TStyle>(EditRecorder recorder, StyleCollection<TStyle> styles, Func<TStyle, bool> affected, Action<TStyle> change) where TStyle : DocumentStyle
    {
        var chosen = styles.All.Where(affected).ToList();
        if (chosen.Count == 0) return;
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        foreach (var style in chosen)
        {
            var copy = (TStyle)style.Clone();
            change(copy);
            styles.Replace(copy);
        }
    }

    private IReadOnlyList<ListLevel> Levels(IReadOnlyList<ListLevel> levels, string name, string? target) =>
        [.. levels.Select(level => level.CharacterStyleName == name ? level with { CharacterStyleName = target } : level)];

    private void Indexes(TextDocument document, EditRecorder recorder, string name, string? target)
    {
        foreach (var section in stories.Stories(document).SelectMany(tree.Blocks).OfType<SectionBlock>().Where(section => section.Index?.LevelStyles.Mentions(name) == true).ToList())
        {
            recorder.Touch<IndexDefinition>(section, () => [section.Index!], definitions => section.Index = definitions[0]);
            section.Index = section.Index! with { LevelStyles = section.Index.LevelStyles.Map(style => style == name ? target : style) };
        }
    }

    private void Notes(TextDocument document, EditRecorder recorder, Func<NoteSettings, NoteSettings> change)
    {
        var notes = document.Notes;
        var footnotes = change(notes.FootnoteSettings);
        var endnotes = change(notes.EndnoteSettings);
        if (footnotes == notes.FootnoteSettings && endnotes == notes.EndnoteSettings) return;
        recorder.Touch<NoteSettings>(notes, () => [notes.FootnoteSettings, notes.EndnoteSettings], settings =>
        {
            notes.FootnoteSettings = settings[0];
            notes.EndnoteSettings = settings[1];
        });
        notes.FootnoteSettings = footnotes;
        notes.EndnoteSettings = endnotes;
    }
}
