using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.TextFormat;

namespace PulseOffice.Documents.UI.StyleCatalog;

public interface IFolioStyleDialogs
{
    Task<string?> NameAsync(StyleFamily family, string? suggestion, CancellationToken ct);

    Task<string?> NameAsync(FolioEditor editor, StyleFamily family, string? suggestion, string? basis, CancellationToken ct) =>
        NameAsync(family, suggestion, ct);

    Task<bool> ConfirmDeleteAsync(string title, CancellationToken ct);

    Task<string?> RenameAsync(StyleFamily family, string name, CancellationToken ct);

    Task EditAsync(FolioEditor editor, StyleFamily family, string name, CancellationToken ct);
}

public sealed class FolioStyleDialogs(
    IFolioForms forms,
    ITabbedForms tabbed,
    IFolioCharacterTabs characters,
    IFolioParagraphTabs paragraphs,
    IFolioListForms lists,
    IFolioPageDialog pages,
    IStyleEditing styles,
    IStyleResolver resolver,
    IStyleLineage lineage,
    IFolioStyleNames names,
    ITextFormatPreviews previews,
    ICharacterSampleLayout samples,
    PulseOffice.UI.Controls.StylePanels.IStyleRenamePrompt renames,
    ITextCatalog texts) : IFolioStyleDialogs
{
    public async Task<string?> NameAsync(FolioEditor editor, StyleFamily family, string? suggestion, string? basis, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (basis is null || family is not (StyleFamily.Paragraph or StyleFamily.Character)) return await NameAsync(family, suggestion, ct).ConfigureAwait(true);
        var sheet = editor.Document.Styles;
        var paragraph = family == StyleFamily.Paragraph
            ? resolver.EffectiveParagraph(sheet, new TextParagraph(string.Empty) { StyleName = basis })
            : editor.EffectiveParagraph();
        var run = family == StyleFamily.Character
            ? resolver.EffectiveRun(sheet, new TextParagraph(string.Empty), new TextRun(string.Empty) { StyleName = basis })
            : resolver.EffectiveRun(sheet, paragraph, new TextRun(string.Empty));
        var state = new CharacterFormatState
        {
            Font = run.Font ?? string.Empty,
            Size = run.FontSize ?? 12,
            Bold = run.Bold,
            Italic = run.Italic,
            Underline = run.DoubleUnderline ? UnderlineKind.Double : run.Underline ? UnderlineKind.Single : UnderlineKind.None,
            Color = run.Color,
            SmallCaps = run.SmallCaps,
        };
        var scope = new ParagraphDialogScope(paragraph, [.. sheet.Page.All.Select(page => page.Name)]);
        var tab = new FormTab(texts["folio.styles.new.title"], texts.Format("folio.styles.new.basedOn", names.Title(basis)),
            [new TextField("name", texts["folio.styles.name"], suggestion ?? string.Empty)])
        {
            LivePreview = feed => family == StyleFamily.Paragraph
                ? paragraphs.Preview(feed, scope)
                : previews.Create(feed, new FeedPreview<string>(
                    values => values.Text("name"),
                    name =>
                    {
                        var view = new CharacterSampleView(samples);
                        view.Show(state, name.Length > 0 ? name : texts["textFormat.character.sample"]);
                        return view;
                    },
                    PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
                    110)),
        };
        var values = await tabbed.AskAsync("folio.styles.new.title", [tab], ct).ConfigureAwait(true);
        return values?.Text("name").Trim() is { Length: > 0 } name ? name : null;
    }

    public async Task<string?> NameAsync(StyleFamily family, string? suggestion, CancellationToken ct)
    {
        var values = await forms.AskAsync("folio.styles.new.title", "folio.styles.new.explanation",
            [new TextField("name", texts["folio.styles.name"], suggestion ?? string.Empty)], ct, 420).ConfigureAwait(true);
        return values?.Text("name").Trim() is { Length: > 0 } name ? name : null;
    }

    public Task<string?> RenameAsync(StyleFamily family, string name, CancellationToken ct) =>
        renames.AskAsync(name, "folio.styles.rename.explanation", ct);

    public async Task<bool> ConfirmDeleteAsync(string title, CancellationToken ct) =>
        await forms.AskAsync("folio.styles.delete.title", "folio.styles.delete.explanation",
            [new CheckField("confirm", texts.Format("folio.styles.delete.confirm", title), true)], ct, 420).ConfigureAwait(true) is { } values && values.Flag("confirm");

    public async Task EditAsync(FolioEditor editor, StyleFamily family, string name, CancellationToken ct)
    {
        if (editor.RefusesDocumentEdit()) return;
        var sheet = editor.Document.Styles;
        switch (family)
        {
            case StyleFamily.Paragraph when sheet.Paragraph.Find(name) is { } paragraph:
                await ParagraphAsync(editor, paragraph, ct).ConfigureAwait(true);
                break;
            case StyleFamily.Character when sheet.Character.Find(name) is { } character:
                await CharacterAsync(editor, character, ct).ConfigureAwait(true);
                break;
            case StyleFamily.List when sheet.List.Contains(name):
                var values = await tabbed.AskAsync("command.folio.list.bulletsNumbering.title", lists.ListTabs(sheet, name, 0, editor.Document.Language), ct).ConfigureAwait(true);
                if (values is null) return;
                var (style, levels, _, _) = lists.ReadList(sheet, name, values);
                Commit(editor, () => styles.Change(editor.Document, editor.History, sheet.List, style, list => list.Levels = levels, "list-style"));
                break;
            case StyleFamily.Page when sheet.Page.Contains(name):
                await pages.EditAsync(editor, name, ct).ConfigureAwait(true);
                break;
        }
    }

    private async Task ParagraphAsync(FolioEditor editor, ParagraphStyle style, CancellationToken ct)
    {
        var sheet = editor.Document.Styles;
        var effective = resolver.EffectiveParagraph(sheet, new TextParagraph(string.Empty) { StyleName = style.Name });
        var run = resolver.EffectiveRun(sheet, effective, new TextRun(string.Empty));
        var scope = new ParagraphDialogScope(effective, [.. sheet.Page.All.Select(page => page.Name)]);
        var others = sheet.Paragraph.All.Where(other => other.Name != style.Name).Select(other => other.Name).ToList();
        var parents = lineage.PossibleParents(sheet.Paragraph, style.Name).ToList();
        var partners = sheet.Character.All.Select(other => other.Name).ToList();
        var linkedIndex = style.LinkedStyleName is { } linked && partners.IndexOf(linked) is >= 0 and var at ? at + 2 : 0;
        var organizer = new FormTab(texts["folio.styles.tab.organizer"], texts["folio.styles.organizer.explanation"],
        [
            new TextField("display", texts["folio.styles.name"], names.Title(style)),
            new ChoiceField("parent", texts["folio.styles.parent"], [texts["folio.styles.none"], .. parents.Select(names.Title)], style.ParentName is { } parent ? parents.IndexOf(parent) + 1 : 0),
            new ChoiceField("next", texts["folio.styles.next"], [texts["folio.styles.same"], .. others.Select(names.Title)], style.NextStyleName is { } next ? others.IndexOf(next) + 1 : 0),
            new ChoiceField("link", texts["folio.styles.link"], [texts["folio.styles.none"], texts["folio.styles.link.new"], .. partners.Select(names.Title)], linkedIndex),
            new NumberField("outline", texts["folio.styles.outline"], style.OutlineLevel ?? 0, 0, 10) { Integer = true },
        ])
        {
            LivePreview = feed => paragraphs.Preview(feed, scope),
        };
        var tabs = new List<FormTab> { organizer };
        tabs.AddRange(paragraphs.Tabs(scope).Select(tab => tab with { Fields = [.. tab.Fields.Where(field => field.Key is not ("break" or "pageStyle" or "pageNumber"))] }));
        tabs.AddRange(characters.Tabs(run));
        if (await tabbed.AskAsync("folio.styles.edit.title", tabs, ct, width: 680).ConfigureAwait(true) is not { } values) return;
        var paragraphChange = paragraphs.Read(scope, values);
        var characterChange = characters.Read(run, values);
        var parentIndex = values.Choice("parent", parents.Count + 1);
        var nextIndex = values.Choice("next", others.Count + 1);
        var outline = (int)Math.Round(values.Number("outline", 0));
        var display = values.Text("display").Trim();
        var linkChoice = values.Choice("link", partners.Count + 2);
        StyleLink link = linkChoice switch
        {
            0 => new StyleLink.None(),
            1 => new StyleLink.ToNew(),
            _ => new StyleLink.ToExisting(partners[linkChoice - 2]),
        };
        Commit(editor, () =>
        {
            using var step = editor.History.BeginTransaction("style-edit");
            ChangeParagraph(editor, style, paragraphChange, characterChange, parentIndex == 0 ? null : parents[parentIndex - 1],
                nextIndex == 0 ? null : others[nextIndex - 1], outline, display);
            if (linkChoice != linkedIndex) styles.Link(editor.Document, editor.History, style.Name, link);
        });
    }

    private void ChangeParagraph(FolioEditor editor, ParagraphStyle style, ParagraphProperties paragraphChange, CharacterChange characterChange,
        string? parent, string? next, int outline, string display) =>
        styles.Change(editor.Document, editor.History, editor.Document.Styles.Paragraph, style.Name, edited =>
        {
            edited.Paragraph = paragraphChange.Over(edited.Paragraph);
            edited.Character = characterChange.Apply(edited.Character);
            edited.ParentName = parent;
            edited.NextStyleName = next;
            edited.OutlineLevel = outline == 0 ? null : outline;
            if (display.Length > 0 && display != names.Title(style)) edited.DisplayName = display;
        }, "style-edit");

    private async Task CharacterAsync(FolioEditor editor, CharacterStyle style, CancellationToken ct)
    {
        var sheet = editor.Document.Styles;
        var run = resolver.EffectiveRun(sheet, new TextParagraph(string.Empty), new TextRun(string.Empty) { StyleName = style.Name });
        var parents = lineage.PossibleParents(sheet.Character, style.Name).ToList();
        var partners = sheet.Paragraph.All.Where(other => other.Name != DefaultStyleNames.Standard).Select(other => other.Name).ToList();
        var linkedIndex = style.LinkedStyleName is { } linked && partners.IndexOf(linked) is >= 0 and var at ? at + 1 : 0;
        var tabs = new List<FormTab>
        {
            new(texts["folio.styles.tab.organizer"], texts["folio.styles.organizer.explanation"],
            [
                new TextField("display", texts["folio.styles.name"], names.Title(style)),
                new ChoiceField("parent", texts["folio.styles.parent"], [texts["folio.styles.none"], .. parents.Select(names.Title)], style.ParentName is { } parent ? parents.IndexOf(parent) + 1 : 0),
                new ChoiceField("link", texts["folio.styles.link.paragraph"], [texts["folio.styles.none"], .. partners.Select(names.Title)], linkedIndex),
            ]),
        };
        tabs.AddRange(characters.Tabs(run));
        if (await tabbed.AskAsync("folio.styles.edit.title", tabs, ct).ConfigureAwait(true) is not { } values) return;
        var change = characters.Read(run, values);
        var parentIndex = values.Choice("parent", parents.Count + 1);
        var display = values.Text("display").Trim();
        var linkChoice = values.Choice("link", partners.Count + 1);
        Commit(editor, () =>
        {
            using var step = editor.History.BeginTransaction("style-edit");
            styles.Change(editor.Document, editor.History, sheet.Character, style.Name, edited =>
            {
                edited.Character = change.Apply(edited.Character);
                edited.ParentName = parentIndex == 0 ? null : parents[parentIndex - 1];
                if (display.Length > 0 && display != names.Title(style)) edited.DisplayName = display;
            }, "style-edit");
            if (linkChoice == linkedIndex) return;
            if (linkChoice > 0) styles.Link(editor.Document, editor.History, partners[linkChoice - 1], new StyleLink.ToExisting(style.Name));
            else if (style.LinkedStyleName is { } previous) styles.Link(editor.Document, editor.History, previous, new StyleLink.None());
        });
    }

    private void Commit(FolioEditor editor, Action change)
    {
        if (!editor.RefusesDocumentEdit()) editor.Change(change);
    }
}
