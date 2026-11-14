using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.TextFormat;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioListForms
{
    IReadOnlyList<FormTab> ListTabs(StyleSheet sheet, string listStyle, int level);

    (string Style, IReadOnlyList<ListLevel> Levels, int Index, bool Apply) ReadList(StyleSheet sheet, string listStyle, IReadOnlyDictionary<string, object> values);

    IReadOnlyList<FormTab> OutlineTabs(ListStyle outline);

    ListStyle ReadOutline(ListStyle outline, IReadOnlyDictionary<string, object> values);
}

public sealed class FolioListForms(IListFormatTabs shared, ITextCatalog texts) : IFolioListForms
{
    private readonly ListNumbering[] _all = [ListNumbering.None, ListNumbering.Bullet, ListNumbering.Arabic, ListNumbering.LowerLetter, ListNumbering.UpperLetter, ListNumbering.LowerRoman, ListNumbering.UpperRoman];

    public IReadOnlyList<FormTab> ListTabs(StyleSheet sheet, string listStyle, int level) =>
        [shared.Tab(ListRequest(sheet, listStyle, level), [new CheckField("apply", texts["folio.list.apply"], true)])];

    public (string Style, IReadOnlyList<ListLevel> Levels, int Index, bool Apply) ReadList(StyleSheet sheet, string listStyle, IReadOnlyDictionary<string, object> values)
    {
        var style = sheet.List.Find(listStyle) ?? sheet.List.All[0];
        var answer = shared.Read(ListRequest(sheet, style.Name, 0), values);
        return (style.Name, Merge(style.Levels, answer.Levels), answer.Level, values.Flag("apply"));
    }

    public IReadOnlyList<FormTab> OutlineTabs(ListStyle outline) => [shared.Tab(OutlineRequest(outline), [])];

    public ListStyle ReadOutline(ListStyle outline, IReadOnlyDictionary<string, object> values) =>
        new(outline.Name, Merge(outline.Levels, shared.Read(OutlineRequest(outline), values).Levels));

    private ListFormatRequest ListRequest(StyleSheet sheet, string listStyle, int level)
    {
        var levels = (sheet.List.Find(listStyle) ?? sheet.List.All[0]).Levels;
        return new ListFormatRequest([.. levels.Select(Format)], Math.Clamp(level, 0, levels.Count - 1), _all, ListFeatures.All);
    }

    private ListFormatRequest OutlineRequest(ListStyle outline) =>
        new([.. outline.Levels.Select(Format)], 0, [.. _all.Where(numbering => numbering != ListNumbering.Bullet)],
            ListFeatures.Affixes | ListFeatures.Start | ListFeatures.DisplayLevels | ListFeatures.Indents, ListSampleMode.Headings);

    private ListLevelFormat Format(ListLevel level) => new((ListNumbering)(int)level.Format)
    {
        Bullet = level.Bullet,
        Prefix = level.Prefix,
        Suffix = level.Suffix,
        Start = level.StartAt,
        DisplayLevels = level.DisplayLevels,
        Indent = level.Indent,
        Hanging = level.Hanging,
        Picture = level.Picture,
    };

    private IReadOnlyList<ListLevel> Merge(IReadOnlyList<ListLevel> existing, IReadOnlyList<ListLevelFormat> edited) =>
        [.. existing.Select((level, index) => index >= edited.Count ? level : level with
        {
            Format = (NumberFormat)(int)edited[index].Numbering,
            Bullet = edited[index].Bullet,
            Prefix = edited[index].Prefix,
            Suffix = edited[index].Suffix,
            StartAt = edited[index].Start,
            DisplayLevels = edited[index].DisplayLevels,
            Indent = edited[index].Indent,
            Hanging = edited[index].Hanging,
            Picture = edited[index].Picture,
        })];
}

public sealed class FolioListCommands(IActiveFolioContext context, IListEditing lists, IStyleEditing styles, IFolioListForms forms, ITabbedForms dialogs)
    : FolioCommandSet(context)
{
    private readonly IReadOnlyList<(string Id, string Style)> _styles =
    [
        ("folio.list.style.bullet", DefaultStyleNames.List1),
        ("folio.list.style.number", DefaultStyleNames.Numbering123),
        ("folio.list.style.alpha", DefaultStyleNames.NumberingAbcUpper),
        ("folio.list.style.alphaLow", DefaultStyleNames.NumberingAbc),
        ("folio.list.style.roman", DefaultStyleNames.NumberingIvxUpper),
        ("folio.list.style.romanLow", DefaultStyleNames.NumberingIvx),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Shift("folio.list.demote", 1, false, null),
        Shift("folio.list.promote", -1, false, null),
        Shift("folio.list.demoteSubpoints", 1, true, null),
        Shift("folio.list.promoteSubpoints", -1, true, null),
        Move("folio.list.moveUp", -1, false, "Ctrl+Alt+Up"),
        Move("folio.list.moveDown", 1, false, "Ctrl+Alt+Down"),
        Move("folio.list.moveUpSubpoints", -1, true, null),
        Move("folio.list.moveDownSubpoints", 1, true, null),
        InList("folio.list.unnumbered") with
        {
            CheckedWhen = _ => Editor?.CurrentParagraph() is { ListStyleName: not null, ListHeader: true },
            Execute = (_, _) => Run(editor => lists.SetNumbered(editor.Document, editor.History, editor.ParagraphRange(), editor.CurrentParagraph().ListHeader)),
        },
        InList("folio.list.restart") with
        {
            CheckedWhen = _ => Editor?.CurrentParagraph().ListRestart is not null,
            Execute = (_, _) => Run(editor =>
            {
                var current = editor.CurrentParagraph();
                var start = editor.Document.Styles.List.Find(current.ListStyleName)?.Level(current.BulletLevel).StartAt ?? 1;
                lists.Restart(editor.Document, editor.History, editor.ParagraphRange(), current.ListRestart is null ? start : null);
            }),
        },
        Editing("folio.list.continue") with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && lists.CanContinue(editor.Document, editor.ParagraphRange()),
            Execute = (_, _) => Run(editor => lists.Continue(editor.Document, editor.History, editor.ParagraphRange())),
        },
        InList("folio.list.none") with
        {
            Execute = (_, _) => Run(editor => editor.Services.Editor.SetList(editor.Document, editor.History, editor.ParagraphRange(), null, 0)),
        },
        Jump("folio.list.nextInLevel", 1),
        Jump("folio.list.previousInLevel", -1),
        .. _styles.Select(ApplyStyle),
        Editing("folio.list.bulletsNumbering") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => ListAsync(ct) },
        Editing("folio.list.chapterNumbering") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => OutlineAsync(ct) },
    ];

    private CommandDefinition InList(string id) =>
        Editing(id) with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.CurrentParagraph().ListStyleName is not null,
        };

    private CommandDefinition Shift(string id, int delta, bool withSubpoints, string? gesture) =>
        Editing(id) with
        {
            DefaultGesture = gesture,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && (editor.CurrentParagraph().ListStyleName is not null || editor.EffectiveParagraph().OutlineLevel is not null),
            Execute = (_, _) => Run(editor => lists.ShiftLevel(editor.Document, editor.History, editor.ParagraphRange(), delta, withSubpoints)),
        };

    private CommandDefinition Move(string id, int direction, bool withSubpoints, string? gesture) =>
        Editing(id) with
        {
            DefaultGesture = gesture,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && lists.CanMove(editor.Document, editor.ParagraphRange(), direction, withSubpoints),
            Execute = (_, _) => Run(editor => lists.Move(editor.Document, editor.History, editor.ParagraphRange(), direction, withSubpoints)),
        };

    private CommandDefinition Jump(string id, int direction) =>
        Viewing(id) with
        {
            EnabledWhen = _ => Editor is { } editor && lists.NextInLevel(editor.Document, editor.Caret.ParagraphId, direction) is not null,
            Execute = (_, _) =>
            {
                if (Editor is { } editor && lists.NextInLevel(editor.Document, editor.Caret.ParagraphId, direction) is { } target)
                    editor.PlaceCaret(new DocumentPosition(target, 0), false);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition ApplyStyle((string Id, string Style) entry) =>
        Editing(entry.Id) with
        {
            CheckedWhen = _ => Editor?.CurrentParagraph().ListStyleName == entry.Style,
            Execute = (_, _) => Run(editor =>
            {
                if (!editor.Document.Styles.List.Contains(entry.Style)) return;
                var current = editor.CurrentParagraph();
                editor.Services.Editor.SetList(editor.Document, editor.History, editor.ParagraphRange(), current.ListStyleName == entry.Style ? null : entry.Style, current.BulletLevel);
            }),
        };

    private Task Run(Action<FolioEditor> change)
    {
        if (Editor is { } editor) editor.Change(() => change(editor));
        return Task.CompletedTask;
    }

    private async Task ListAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || editor.Document.Styles.List.All.Count == 0) return;
        var current = editor.CurrentParagraph();
        var sheet = editor.Document.Styles;
        var style = current.ListStyleName ?? editor.EffectiveParagraph().ListStyleName ?? DefaultStyleNames.Numbering123;
        if (await dialogs.AskAsync("command.folio.list.bulletsNumbering.title", forms.ListTabs(sheet, style, current.BulletLevel), ct).ConfigureAwait(true) is not { } values) return;
        var (name, levels, index, apply) = forms.ReadList(sheet, style, values);
        editor.Change(() =>
        {
            using var transaction = editor.History.BeginTransaction("bullets-numbering");
            if (sheet.List.Find(name) is { } found && !found.Levels.SequenceEqual(levels))
                styles.Change(editor.Document, editor.History, sheet.List, name, list => list.Levels = levels, "list-style");
            if (apply) editor.Services.Editor.SetList(editor.Document, editor.History, editor.ParagraphRange(), name, index);
        });
    }

    private async Task OutlineAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var outline = editor.Document.Styles.Outline;
        if (await dialogs.AskAsync("command.folio.list.chapterNumbering.title", forms.OutlineTabs(outline), ct).ConfigureAwait(true) is not { } values) return;
        var changed = forms.ReadOutline(outline, values);
        if (changed.Levels.SequenceEqual(outline.Levels)) return;
        editor.Change(() => styles.ChangeOutline(editor.Document, editor.History, changed, "chapter-numbering"));
    }
}
