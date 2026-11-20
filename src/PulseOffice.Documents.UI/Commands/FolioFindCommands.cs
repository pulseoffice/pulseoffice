using System.Globalization;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioFindServices(
    ISearchStories Stories,
    ISearchReplacing Replacing,
    IFolioFindHighlights Highlights,
    ISidePanelHost Panel,
    IFindPanelPages Pages,
    ICommentAnchors Anchors);

public sealed class FolioFindCommands(
    IActiveFolioContext context,
    IDocumentSearch search,
    IDocumentStatisticsCounter statistics,
    IStatusBarService status,
    IDialogService dialogs,
    ITextCatalog texts,
    FolioFindServices find) : FolioCommandSet(context)
{
    private SearchQuery? _last;
    private (SearchMatch Match, DocumentRange Shown)? _shownComment;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.edit.find") with { DefaultGesture = "Ctrl+F", Execute = (_, _) => ShowPanel() },
        Viewing("folio.edit.findReplace") with { DefaultGesture = "Ctrl+Shift+H", Execute = (_, ct) => FindAsync(null, ct) },
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

    private Task ShowPanel()
    {
        find.Pages.Show(find.Panel, () => new FolioFindPanelSource(this));
        return Task.CompletedTask;
    }

    public FolioFindTarget? Target(string initial = "", bool withFormats = true) =>
        Editor is { } editor ? new FolioFindTarget(editor, this, texts, initial, _last, withFormats) : null;

    public void Highlight(SearchQuery? query)
    {
        if (Editor is not { } editor) return;
        if (query is null || query.IsEmpty || !search.IsValid(query)) find.Highlights.Clear(editor);
        else find.Highlights.Show(editor, query);
    }

    public Task FindAsync(string? query, CancellationToken ct)
    {
        if (Editor is not { } editor) return Task.CompletedTask;
        var initial = query ?? _last?.Text ?? (editor.HasSelection ? editor.SelectedText().Split('\n')[0] : string.Empty);
        var target = new FolioFindTarget(editor, this, texts, initial, _last);
        return dialogs.ShowAsync<object>(
            new FindReplaceView(target, texts),
            new DialogOptions { TitleKey = "command.folio.edit.findReplace.title", PreferredWidth = 520 },
            ct);
    }

    private IReadOnlyList<SearchStory> Stories(FolioEditor editor) => find.Stories.From(editor.Document, editor.Caret.ParagraphId);

    public int ReplaceAll(FolioEditor editor, SearchQuery query, string replacement, SearchFormat? format = null)
    {
        if (editor.ReadOnly()) return 0;
        _last = query;
        List<(SearchMatch Match, string Text)> found = [.. search.Matches(Stories(editor), query)
            .Where(match => Editable(editor, match.Range))
            .Select(match => (match, search.Replacement(match, query, replacement)))];
        if (found.Count == 0) return 0;
        using (editor.History.BeginTransaction("replace-all"))
        {
            foreach (var (match, text) in Enumerable.Reverse(found))
                find.Replacing.Replace(editor.Services.Editor, editor.Document, editor.History, match, text, format);
        }
        editor.Normalize();
        editor.Select(editor.Caret, editor.Caret);
        return found.Count;
    }

    public bool Replace(FolioEditor editor, SearchQuery query, string replacement, SearchFormat? format = null)
    {
        _last = query;
        if (!editor.ReadOnly() && editor.HasSelection && editor.Selection is { } selected
            && search.Matches(Stories(editor), query).FirstOrDefault(match => match.Range == selected && match.CommentId is null) is { } match
            && Editable(editor, selected))
        {
            var text = search.Replacement(match, query, replacement);
            DocumentRange inserted;
            using (editor.History.BeginTransaction("replace"))
                inserted = find.Replacing.Replace(editor.Services.Editor, editor.Document, editor.History, match, text, format);
            editor.Normalize();
            editor.Select(inserted.End, inserted.End);
        }
        return Next(editor, query);
    }

    public int Count(FolioEditor editor, SearchQuery query) => search.Matches(Stories(editor), query).Count;

    public bool IsValid(SearchQuery query) => search.IsValid(query);

    private bool Editable(FolioEditor editor, DocumentRange range) =>
        !editor.Shielded(range)
        && editor.Services.Order.Between(editor.Document, range.Start, range.End)
            .All(paragraph => editor.Services.Guard.Check(editor.Document, paragraph.Id) == PulseOffice.Documents.Protection.EditVerdict.Allowed);

    public bool Next(FolioEditor editor, SearchQuery query, bool backwards = false)
    {
        _last = query;
        var stories = Stories(editor);
        var matches = search.Matches(stories, query)
            .Where(match => match.CommentId is null || find.Anchors.Find(editor.Document, match.CommentId) is not null).ToList();
        var from = backwards ? (editor.HasSelection ? editor.Selection.Start : editor.Caret) : editor.HasSelection ? editor.Selection.End : editor.Caret;
        if (_shownComment is { } shown && editor.HasSelection && editor.Selection == shown.Shown)
            from = backwards ? shown.Match.Range.Start : shown.Match.Range.End;
        _shownComment = null;
        find.Highlights.Focus(editor, null);
        if (search.Next(matches, stories, from, backwards) is not { } found)
        {
            status.Report(texts.Format("folio.find.notFound", query.Text), NotificationSeverity.Warning);
            return false;
        }
        if (found.CommentId is { } commentId && find.Anchors.Find(editor.Document, commentId) is { } anchor)
        {
            var shownRange = new DocumentRange(anchor.Start, anchor.End);
            editor.Select(anchor.Start, anchor.End);
            _shownComment = (found, shownRange);
            find.Highlights.Focus(editor, found);
            status.Report(texts.Format("folio.find.inComment", anchor.Comment.Author), NotificationSeverity.Info);
            return true;
        }
        editor.Select(found.Range.Start, found.Range.End);
        return true;
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
