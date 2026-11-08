using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fonts;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioDialogCommands(IActiveFolioContext context, IFolioForms forms, IFolioCharacterDialog characters, IFolioParagraphDialog paragraphs, IDirectFormatting direct, ITextCatalog texts)
    : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.format.character") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => CharacterAsync(ct) },
        Editing("folio.format.paragraph") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => ParagraphAsync(ct) },
        Editing("folio.format.tabs") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => ParagraphAsync(FolioParagraphTabs.TabsTab, ct) },
        Editing("folio.format.borders") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => ParagraphAsync(FolioParagraphTabs.BordersTab, ct) },
        Editing("folio.format.dropCap") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => ParagraphAsync(FolioParagraphTabs.DropCapTab, ct) },
    ];

    public async Task CharacterAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        if (await characters.AskAsync(editor.EffectiveRun(), editor.SelectedText(), "command.folio.format.character.title", ct).ConfigureAwait(true) is not { IsEmpty: false } change) return;
        editor.Apply((documents, range) => documents.MapRuns(editor.Document, editor.History, range, "character-format", run => change.Apply(direct, run)), editor.CharacterRange());
    }

    public async Task ParagraphAsync(CancellationToken ct) => await ParagraphAsync(0, ct).ConfigureAwait(true);

    public async Task ParagraphAsync(int tab, CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var scope = new ParagraphDialogScope(editor.EffectiveParagraph(), [.. editor.Document.Styles.Page.All.Select(style => style.Name)])
        {
            DefaultTabInterval = editor.Document.Styles.DefaultTabInterval,
        };
        if (await paragraphs.AskAsync(scope, "command.folio.format.paragraph.title", ct, tab).ConfigureAwait(true) is not { } answer) return;
        if (answer.DefaultTabInterval is { } interval) DefaultTabs(editor, interval);
        if (answer.IsEmpty) return;
        editor.Apply((documents, range) =>
        {
            using var transaction = editor.History.BeginTransaction("paragraph-format");
            if (answer.Properties != ParagraphProperties.None) documents.SetParagraph(editor.Document, editor.History, range, answer.Properties);
            if (answer.PageClear is { } clear) ClearPageStart(editor, range, clear);
        }, editor.ParagraphRange());
    }

    private void ClearPageStart(FolioEditor editor, PulseOffice.Documents.Editing.DocumentRange range, ParagraphPageClear clear)
    {
        var starting = editor.Services.Order.Between(editor.Document, range.Start, range.End)
            .Where(paragraph => clear.Style && paragraph.Content.PageStyleName is not null || clear.Restart && paragraph.Content.PageNumberRestart is not null)
            .ToList();
        if (starting.Count == 0) return;
        var recorder = new PulseOffice.Documents.Editing.EditRecorder(editor.Document, "page-start");
        foreach (var paragraph in starting)
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with
            {
                Content = paragraph.Content with
                {
                    PageStyleName = clear.Style ? null : paragraph.Content.PageStyleName,
                    PageNumberRestart = clear.Restart ? null : paragraph.Content.PageNumberRestart,
                },
            };
        }
        recorder.Commit(editor.History);
    }

    private void DefaultTabs(FolioEditor editor, double interval)
    {
        var document = editor.Document;
        var before = document.Styles.DefaultTabInterval;
        document.Styles.DefaultTabInterval = interval;
        editor.History.Push(new PulseOffice.Document.Commands.DelegateAction(texts["folio.paragraph.defaultTabs"],
            () => { document.Styles.DefaultTabInterval = interval; document.Raise(); }, () => { document.Styles.DefaultTabInterval = before; document.Raise(); }));
        document.Raise();
    }

    public async Task StyleAsync(IReadOnlyList<RibbonChoice> choices, CancellationToken ct)
    {
        if (Editor is not { } editor || choices.Count == 0) return;
        var current = editor.CurrentParagraph().StyleName ?? DefaultStyleNames.Standard;
        var fields = new List<FormField>
        {
            new ChoiceField("style", texts["folio.style.field"], [.. choices.Select(choice => choice.Title)],
                Math.Max(0, choices.ToList().FindIndex(choice => choice.Key == current))),
        };
        if (await forms.AskAsync("command.folio.style.paragraph.title", "folio.style.explanation", fields, ct).ConfigureAwait(true) is not { } values) return;
        var name = choices[values.Choice("style", choices.Count)].Key;
        editor.Apply((documents, range) => documents.ApplyParagraphStyle(editor.Document, editor.History, range, name), editor.ParagraphRange());
    }
}
