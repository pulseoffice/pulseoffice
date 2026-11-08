using Avalonia.Threading;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Navigation;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Navigation;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioNavigation
{
    bool Go(FolioEditor editor, NavigationTarget target);

    bool Follow(FolioEditor editor, string link);
}

public sealed class FolioNavigation(IDocumentNavigation navigation) : IFolioNavigation
{
    public bool Go(FolioEditor editor, NavigationTarget target)
    {
        if (target.ObjectId is { } objectId && editor.Document.Objects.Find(objectId) is not null)
        {
            editor.SelectObject(objectId);
            editor.Refresh();
            return true;
        }
        if (target.ParagraphId is not { } paragraph) return false;
        try
        {
            editor.PlaceCaret(new DocumentPosition(paragraph, target.Offset), false);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        editor.Refresh();
        return true;
    }

    public bool Follow(FolioEditor editor, string link) =>
        navigation.Resolve(editor.Document, link) is { } target && Go(editor, target);
}

public sealed class FolioNavigatorSource : INavigatorSource
{
    private readonly IActiveFolioContext _context;
    private readonly IDocumentNavigation _navigation;
    private readonly IChapterMoving _chapters;
    private readonly IFolioNavigation _go;
    private readonly ITextCatalog _texts;
    private readonly DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private FolioEditor? _watched;

    public FolioNavigatorSource(IActiveFolioContext context, IDocumentNavigation navigation, IChapterMoving chapters, IFolioNavigation go, ITextCatalog texts)
    {
        _context = context;
        _navigation = navigation;
        _chapters = chapters;
        _go = go;
        _texts = texts;
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };
        context.Changed += (_, _) => Watch();
        Watch();
    }

    public event EventHandler? Changed;

    private FolioEditor? Editor => _context.Target?.Editor;

    public IReadOnlyList<NavigatorCategory> Categories =>
        [.. Enum.GetValues<NavigationCategory>().Select(category => new NavigatorCategory(category.ToString(), _texts["folio.navigator." + category.ToString().ToLowerInvariant()]))];

    public IReadOnlyList<NavigatorItem> Items(string category)
    {
        if (Editor is not { } editor || !Enum.TryParse<NavigationCategory>(category, out var kind)) return [];
        var entries = _navigation.Entries(editor.Document, kind);
        return [.. entries.Select(entry => new NavigatorItem(entry.Id, entry.Title, entry.Depth)
        {
            Movable = kind == NavigationCategory.Headings && _chapters.IsChapter(editor.Document, entry.Id),
        })];
    }

    public string? Current(string category)
    {
        if (Editor is not { } editor || category != nameof(NavigationCategory.Headings)) return null;
        var caret = editor.Caret;
        var indexes = editor.Services.Order.Indexes(editor.Document);
        var caretIndex = indexes.GetValueOrDefault(caret.ParagraphId, -1);
        string? current = null;
        foreach (var entry in _navigation.Entries(editor.Document, NavigationCategory.Headings))
        {
            if (entry.Id == caret.ParagraphId) return entry.Id;
            var entryIndex = indexes.GetValueOrDefault(entry.Id, -1);
            if (entryIndex > caretIndex) break;
            current = entry.Id;
        }
        return current;
    }

    public void Go(string category, string id)
    {
        if (Editor is not { } editor || !Enum.TryParse<NavigationCategory>(category, out var kind)) return;
        if (_navigation.Entries(editor.Document, kind).FirstOrDefault(entry => entry.Id == id) is { } found) _go.Go(editor, found.Target);
    }

    public bool CanMove(string category, string id, NavigatorMove move) =>
        category == nameof(NavigationCategory.Headings) && Editor is { } editor && !editor.ReadOnly() && Editable(editor, id) && _chapters.IsChapter(editor.Document, id);

    public void Move(string category, string id, NavigatorMove move)
    {
        if (!CanMove(category, id, move) || Editor is not { } editor) return;
        editor.Change(() =>
        {
            _ = move switch
            {
                NavigatorMove.Up => _chapters.MoveUp(editor.Document, editor.History, id),
                NavigatorMove.Down => _chapters.MoveDown(editor.Document, editor.History, id),
                NavigatorMove.Promote => _chapters.Shift(editor.Document, editor.History, id, -1),
                _ => _chapters.Shift(editor.Document, editor.History, id, 1),
            };
        });
    }

    public bool CanDrop(string category, string id, string? beforeId) =>
        CanMove(category, id, NavigatorMove.Up) && (beforeId is null || Editable(Editor!, beforeId) && _chapters.IsChapter(Editor!.Document, beforeId));

    public void Drop(string category, string id, string? beforeId)
    {
        if (!CanDrop(category, id, beforeId) || Editor is not { } editor) return;
        editor.Change(() => _chapters.Move(editor.Document, editor.History, id, beforeId));
    }

    private bool Editable(FolioEditor editor, string paragraphId) =>
        editor.Services.Guard.Check(editor.Document, paragraphId) == PulseOffice.Documents.Protection.EditVerdict.Allowed;

    private void Watch()
    {
        if (_watched is not null) _watched.Changed -= OnEdited;
        _watched = Editor;
        if (_watched is not null) _watched.Changed += OnEdited;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnEdited(object? sender, EventArgs args)
    {
        _settle.Stop();
        _settle.Start();
    }
}
