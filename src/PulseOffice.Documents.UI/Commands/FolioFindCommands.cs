using System.Globalization;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioFindCommands(
    IActiveFolioContext context,
    IDocumentSearch search,
    IDocumentStatisticsCounter statistics,
    IStatusBarService status,
    IDialogService dialogs,
    ITextCatalog texts) : FolioCommandSet(context)
{
    private SearchQuery? _last;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.edit.findReplace") with { DefaultGesture = "Ctrl+F", Execute = (_, ct) => FindAsync(ct) },
        Viewing("folio.edit.findNext") with
        {
            DefaultGesture = "F3",
            Surface = CommandSurface.Menu | CommandSurface.Palette,
            EnabledWhen = _ => Editor is not null && _last is not null,
            Execute = (_, _) =>
            {
                if (Editor is { } editor && _last is { } query) _ = Next(editor, query);
                return Task.CompletedTask;
            },
        },
        Viewing("folio.tools.wordCount") with { Execute = (_, ct) => CountAsync(ct) },
    ];

    private Task FindAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return Task.CompletedTask;
        var initial = _last?.Text ?? (editor.HasSelection ? editor.SelectedText().Split('\n')[0] : string.Empty);
        var target = new FolioFindTarget(editor, this, texts, initial, _last);
        return dialogs.ShowAsync<object>(
            new FindReplaceView(target, texts),
            new DialogOptions { TitleKey = "command.folio.edit.findReplace.title", PreferredWidth = 480 },
            ct);
    }

    public int ReplaceAll(FolioEditor editor, SearchQuery query, string replacement)
    {
        if (editor.ReadOnly()) return 0;
        _last = query;
        var story = editor.Services.Order.StoryOf(editor.Document, editor.Caret.ParagraphId);
        List<DocumentRange> found = [.. search.FindAll(story, query).Where(range => Editable(editor, range))];
        if (found.Count == 0) return 0;
        using (editor.History.BeginTransaction("replace-all"))
        {
            foreach (var range in Enumerable.Reverse(found))
            {
                editor.Services.Editor.Delete(editor.Document, editor.History, range);
                if (replacement.Length > 0) editor.Services.Editor.InsertText(editor.Document, editor.History, range.Start, replacement);
            }
        }
        editor.Normalize();
        editor.Select(editor.Caret, editor.Caret);
        return found.Count;
    }

    private bool Editable(FolioEditor editor, DocumentRange range) =>
        !editor.Shielded(range)
        && editor.Services.Order.Between(editor.Document, range.Start, range.End)
            .All(paragraph => editor.Services.Guard.Check(editor.Document, paragraph.Id) == PulseOffice.Documents.Protection.EditVerdict.Allowed);

    public bool Next(FolioEditor editor, SearchQuery query)
    {
        _last = query;
        var story = editor.Services.Order.StoryOf(editor.Document, editor.Caret.ParagraphId);
        var after = editor.HasSelection ? editor.Selection.End : editor.Caret;
        if (search.FindNext(story, query, after) is { } range)
        {
            editor.Select(range.Start, range.End);
            return true;
        }
        status.Report(texts.Format("folio.find.notFound", query.Text), NotificationSeverity.Warning);
        return false;
    }

    private async Task CountAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var story = editor.Services.Order.StoryOf(editor.Document, editor.Caret.ParagraphId);
        var whole = statistics.Count(story.Select(paragraph => paragraph.Text));
        var selected = editor.HasSelection ? statistics.Count(editor.SelectedText().Split('\n')) : null;
        StatisticsRow[] rows =
        [
            new(texts["folio.count.words"], Number(whole.Words), Optional(selected?.Words)),
            new(texts["folio.count.characters"], Number(whole.Characters), Optional(selected?.Characters)),
            new(texts["folio.count.charactersNoSpaces"], Number(whole.CharactersWithoutSpaces), Optional(selected?.CharactersWithoutSpaces)),
            new(texts["folio.count.paragraphs"], Number(whole.Paragraphs), Optional(selected?.Paragraphs)),
            new(texts["folio.count.pages"], Number(editor.Layout.Pages.Count), selected is null ? null : string.Empty),
        ];
        await dialogs.ShowAsync<object>(
            new StatisticsView(rows, texts["folio.count.document"], selected is null ? null : texts["folio.count.selectionHeader"], texts),
            new DialogOptions { TitleKey = "command.folio.tools.wordCount.title", PreferredWidth = selected is null ? 360 : 440 },
            ct).ConfigureAwait(true);
    }

    private string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private string? Optional(int? value) => value is { } number ? Number(number) : null;
}
