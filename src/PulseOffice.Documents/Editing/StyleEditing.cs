using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public interface IStyleEditing
{
    bool Change<TStyle>(TextDocument document, UndoStack history, StyleCollection<TStyle> styles, string name, Action<TStyle> change, string description)
        where TStyle : DocumentStyle;

    bool Add<TStyle>(TextDocument document, UndoStack history, StyleCollection<TStyle> styles, TStyle style, string description)
        where TStyle : DocumentStyle;

    bool Remove<TStyle>(TextDocument document, UndoStack history, StyleCollection<TStyle> styles, string name, string description)
        where TStyle : DocumentStyle;

    bool ChangeOutline(TextDocument document, UndoStack history, ListStyle outline, string description);

    DocumentEdit Delete(TextDocument document, UndoStack history, StyleFamily family, string name);

    DocumentEdit Rename(TextDocument document, UndoStack history, StyleFamily family, string name, string newName);

    DocumentEdit Load(TextDocument document, UndoStack history, StyleSheet source, bool overwrite);

    DocumentEdit Link(TextDocument document, UndoStack history, string paragraphStyle, StyleLink link);

    PageStyle PageCopy(TextDocument document, PageStyle source, string name);
}

public sealed class StyleEditing(IStyleReferences references, IFragmentRemapper remapper) : IStyleEditing
{
    public bool Change<TStyle>(TextDocument document, UndoStack history, StyleCollection<TStyle> styles, string name, Action<TStyle> change, string description)
        where TStyle : DocumentStyle
    {
        if (styles.Find(name) is not { } existing) return false;
        var before = (TStyle)existing.Clone();
        var after = (TStyle)existing.Clone();
        change(after);
        var partner = Partner(document.Styles, before, after);
        partner?.Redo();
        Apply(document, styles, (TStyle)after.Clone());
        history.Push(new DelegateAction(description,
            () =>
            {
                partner?.Redo();
                Apply(document, styles, (TStyle)after.Clone());
            },
            () =>
            {
                partner?.Undo();
                Apply(document, styles, (TStyle)before.Clone());
            }));
        return true;
    }

    private sealed record PartnerChange(Action Redo, Action Undo);

    private PartnerChange? Partner(StyleSheet sheet, DocumentStyle before, DocumentStyle after)
    {
        switch (before, after)
        {
            case (ParagraphStyle old, ParagraphStyle edited) when old.Character != edited.Character && edited.LinkedStyleName is { } name && sheet.Character.Find(name) is { } partner && partner.LinkedStyleName == edited.Name:
                var characterBefore = (CharacterStyle)partner.Clone();
                var characterAfter = (CharacterStyle)partner.Clone();
                characterAfter.Character = edited.Character;
                return new PartnerChange(() => sheet.Character.Replace((CharacterStyle)characterAfter.Clone()), () => sheet.Character.Replace((CharacterStyle)characterBefore.Clone()));
            case (CharacterStyle old, CharacterStyle edited) when old.Character != edited.Character && edited.LinkedStyleName is { } name && sheet.Paragraph.Find(name) is { } partner && partner.LinkedStyleName == edited.Name:
                var paragraphBefore = (ParagraphStyle)partner.Clone();
                var paragraphAfter = (ParagraphStyle)partner.Clone();
                paragraphAfter.Character = edited.Character;
                return new PartnerChange(() => sheet.Paragraph.Replace((ParagraphStyle)paragraphAfter.Clone()), () => sheet.Paragraph.Replace((ParagraphStyle)paragraphBefore.Clone()));
            default:
                return null;
        }
    }

    public bool Add<TStyle>(TextDocument document, UndoStack history, StyleCollection<TStyle> styles, TStyle style, string description)
        where TStyle : DocumentStyle
    {
        if (styles.Contains(style.Name)) return false;
        var added = (TStyle)style.Clone();
        styles.Add((TStyle)added.Clone());
        document.Raise();
        history.Push(new DelegateAction(description,
            () =>
            {
                styles.Add((TStyle)added.Clone());
                document.Raise();
            },
            () =>
            {
                styles.Remove(added.Name);
                document.Raise();
            }));
        return true;
    }

    public bool Remove<TStyle>(TextDocument document, UndoStack history, StyleCollection<TStyle> styles, string name, string description)
        where TStyle : DocumentStyle
    {
        if (styles.Find(name) is not { } existing) return false;
        var removed = (TStyle)existing.Clone();
        styles.Remove(name);
        document.Raise();
        history.Push(new DelegateAction(description,
            () =>
            {
                styles.Remove(name);
                document.Raise();
            },
            () =>
            {
                styles.Add((TStyle)removed.Clone());
                document.Raise();
            }));
        return true;
    }

    public bool ChangeOutline(TextDocument document, UndoStack history, ListStyle outline, string description)
    {
        var before = (ListStyle)document.Styles.Outline.Clone();
        var after = (ListStyle)outline.Clone();
        document.Styles.Outline = (ListStyle)after.Clone();
        document.Raise();
        history.Push(new DelegateAction(description,
            () =>
            {
                document.Styles.Outline = (ListStyle)after.Clone();
                document.Raise();
            },
            () =>
            {
                document.Styles.Outline = (ListStyle)before.Clone();
                document.Raise();
            }));
        return true;
    }

    public DocumentEdit Delete(TextDocument document, UndoStack history, StyleFamily family, string name)
    {
        var sheet = document.Styles;
        var recorder = new EditRecorder(document, "style-delete");
        switch (family)
        {
            case StyleFamily.Paragraph when sheet.Paragraph.Find(name) is { } paragraph && name != DefaultStyleNames.Standard:
                references.RewriteStyles(recorder, sheet.Paragraph, style => style.NextStyleName == name, style => style.NextStyleName = null);
                references.RewriteStyles(recorder, sheet.Character, style => style.LinkedStyleName == name, style => style.LinkedStyleName = null);
                references.Redirect(document, recorder, family, name, paragraph.ParentName ?? DefaultStyleNames.Standard);
                Remove(recorder, sheet.Paragraph, name);
                break;
            case StyleFamily.Character when sheet.Character.Find(name) is { } character:
                references.RewriteStyles(recorder, sheet.Paragraph, style => style.LinkedStyleName == name, style => style.LinkedStyleName = null);
                references.Redirect(document, recorder, family, name, character.ParentName);
                Remove(recorder, sheet.Character, name);
                break;
            case StyleFamily.List when sheet.List.Contains(name):
                references.RewriteParagraphs(document, recorder, content => content.ListStyleName == name,
                    content => content with { ListStyleName = null, Bullet = false, Numbered = false, ListHeader = false, ListRestart = null, ListId = null, BulletLevel = 0 });
                references.Redirect(document, recorder, family, name, null);
                Remove(recorder, sheet.List, name);
                break;
            case StyleFamily.Page when sheet.Page.Contains(name) && name != DefaultStyleNames.Standard && name != document.InitialPageStyle:
                references.RewriteStyles(recorder, sheet.Page, style => style.NextStyleName == name, style => style.NextStyleName = null);
                references.Redirect(document, recorder, family, name, DefaultStyleNames.Standard);
                Remove(recorder, sheet.Page, name);
                break;
            case StyleFamily.Frame when sheet.Frame.Find(name) is { } frame:
                references.Redirect(document, recorder, family, name, frame.ParentName);
                Remove(recorder, sheet.Frame, name);
                break;
            case StyleFamily.Table when sheet.Table.Find(name) is { } table:
                references.Redirect(document, recorder, family, name, table.ParentName);
                Remove(recorder, sheet.Table, name);
                break;
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Rename(TextDocument document, UndoStack history, StyleFamily family, string name, string newName)
    {
        var recorder = new EditRecorder(document, "style-rename");
        var target = newName.Trim();
        var sheet = document.Styles;
        if (target.Length == 0 || target == name || target == StyleSheet.OutlineStyleName) return recorder.Commit(history);
        switch (family)
        {
            case StyleFamily.Paragraph:
                Rename(document, recorder, sheet.Paragraph, family, name, target);
                break;
            case StyleFamily.Character:
                Rename(document, recorder, sheet.Character, family, name, target);
                break;
            case StyleFamily.List:
                Rename(document, recorder, sheet.List, family, name, target);
                break;
            case StyleFamily.Page:
                Rename(document, recorder, sheet.Page, family, name, target);
                break;
            case StyleFamily.Frame:
                Rename(document, recorder, sheet.Frame, family, name, target);
                break;
            case StyleFamily.Table:
                Rename(document, recorder, sheet.Table, family, name, target);
                break;
        }
        return recorder.Commit(history);
    }

    private void Rename<TStyle>(TextDocument document, EditRecorder recorder, StyleCollection<TStyle> styles, StyleFamily family, string name, string target) where TStyle : DocumentStyle
    {
        if (styles.Find(name) is not { } style || styles.Contains(target)) return;
        references.Redirect(document, recorder, family, name, target);
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        var renamed = (TStyle)styles.Find(name)!.CloneAs(target);
        if (renamed.ParentName == name) renamed.ParentName = target;
        styles.ReplaceAll([.. styles.All.Select(existing => existing.Name == name ? renamed : existing)]);
    }

    public DocumentEdit Load(TextDocument document, UndoStack history, StyleSheet source, bool overwrite)
    {
        var recorder = new EditRecorder(document, "style-load");
        source = remapper.Remap(document, new DocumentFragment(new BlockContainer(), source, [], [], [])).Styles;
        var added = new List<DocumentStyle>();
        added.AddRange(Load(recorder, document.Styles.Paragraph, source.Paragraph, overwrite));
        added.AddRange(Load(recorder, document.Styles.Character, source.Character, overwrite));
        Load(recorder, document.Styles.List, source.List, overwrite);
        Load(recorder, document.Styles.Page, source.Page, overwrite);
        Load(recorder, document.Styles.Frame, source.Frame, overwrite);
        Load(recorder, document.Styles.Table, source.Table, overwrite);
        document.Styles.DropDanglingLinks(added);
        return recorder.Commit(history);
    }

    public DocumentEdit Link(TextDocument document, UndoStack history, string paragraphStyle, StyleLink link)
    {
        var recorder = new EditRecorder(document, "style-link");
        var sheet = document.Styles;
        if (sheet.Paragraph.Find(paragraphStyle) is not { } paragraph) return recorder.Commit(history);
        var partner = link switch
        {
            StyleLink.ToExisting existing when sheet.Character.Contains(existing.CharacterStyle) => existing.CharacterStyle,
            StyleLink.ToNew => FreeCharacterName(sheet, paragraphStyle + LinkedStyleSelection.PartnerSuffix),
            _ => null,
        };
        if (partner == paragraph.LinkedStyleName && (partner is null || sheet.Character.Find(partner)!.LinkedStyleName == paragraphStyle)) return recorder.Commit(history);
        recorder.Touch(sheet.Character, () => sheet.Character.All, sheet.Character.ReplaceAll);
        recorder.Touch(sheet.Paragraph, () => sheet.Paragraph.All, sheet.Paragraph.ReplaceAll);
        if (link is StyleLink.ToNew && partner is not null)
            sheet.Character.Replace(new CharacterStyle(partner) { Character = paragraph.Character, LinkedStyleName = paragraphStyle });
        sheet.Paragraph.ReplaceAll([.. sheet.Paragraph.All.Select(style =>
            style.Name == paragraphStyle ? Linked(style, partner)
            : partner is not null && style.LinkedStyleName == partner ? Linked(style, null)
            : style)]);
        sheet.Character.ReplaceAll([.. sheet.Character.All.Select(style =>
            style.Name == partner ? Linked(style, paragraphStyle)
            : style.LinkedStyleName == paragraphStyle ? Linked(style, null)
            : style)]);
        return recorder.Commit(history);
    }

    private TStyle Linked<TStyle>(TStyle style, string? partner) where TStyle : DocumentStyle
    {
        var copy = (TStyle)style.Clone();
        switch (copy)
        {
            case ParagraphStyle paragraph:
                paragraph.LinkedStyleName = partner;
                break;
            case CharacterStyle character:
                character.LinkedStyleName = partner;
                break;
        }
        return copy;
    }

    private string FreeCharacterName(StyleSheet sheet, string wanted)
    {
        var name = wanted;
        for (var index = 2; sheet.Character.Contains(name) || sheet.Paragraph.Contains(name); index++) name = $"{wanted} {index}";
        return name;
    }

    public PageStyle PageCopy(TextDocument document, PageStyle source, string name)
    {
        var sheet = new StyleSheet();
        sheet.Page.Replace((PageStyle)source.CloneAs(name));
        var copy = remapper.Remap(document, new DocumentFragment(new BlockContainer(), sheet, [], [], [])).Styles.Page.Find(name)!;
        var created = (PageStyle)copy.CloneAs(name);
        created.ParentName = null;
        created.DisplayName = null;
        created.Hidden = false;
        created.NextStyleName = null;
        return created;
    }

    private List<TStyle> Load<TStyle>(EditRecorder recorder, StyleCollection<TStyle> target, StyleCollection<TStyle> source, bool overwrite) where TStyle : DocumentStyle
    {
        var chosen = source.All.Where(style => overwrite || !target.Contains(style.Name)).Select(style => (TStyle)style.Clone()).ToList();
        if (chosen.Count == 0) return chosen;
        recorder.Touch(target, () => target.All, target.ReplaceAll);
        foreach (var style in chosen) target.Replace(style);
        return chosen;
    }

    private void Remove<TStyle>(EditRecorder recorder, StyleCollection<TStyle> styles, string name) where TStyle : DocumentStyle
    {
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        styles.Remove(name);
    }

    private void Apply<TStyle>(TextDocument document, StyleCollection<TStyle> styles, TStyle style) where TStyle : DocumentStyle
    {
        styles.Replace(style);
        document.Raise();
    }
}
