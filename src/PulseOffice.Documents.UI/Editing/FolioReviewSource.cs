using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Revisions;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Review;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioReviewText
{
    string Describe(TextDocument document, ChangeRegion region);

    string Plain(TextDocument document, DocumentPosition start, DocumentPosition end);

    string? Place(TextDocument document, TrackedChange change);

    string Rows(IReadOnlyList<TrackedRow> rows);
}

public sealed class FolioReviewText(IParagraphOrder order, ITextCatalog texts) : IFolioReviewText
{
    private const int Limit = 400;

    private const string Separator = " ¶ ";

    public string Describe(TextDocument document, ChangeRegion region) => region.Change switch
    {
        Deletion deletion => Clip(deletion.Removed.Blocks.Select(block => block is ParagraphBlock paragraph ? paragraph.Text : texts["review.kind.table"])),
        ParagraphFormatChange => Clip(Pieces(document, region.Start with { Offset = 0 }, region.Start with { Offset = int.MaxValue })),
        _ => Clip(Pieces(document, region.Start, region.End)),
    };

    public string Plain(TextDocument document, DocumentPosition start, DocumentPosition end) => string.Join(Separator, Pieces(document, start, end));

    private IEnumerable<string> Pieces(TextDocument document, DocumentPosition start, DocumentPosition end) =>
        order.Between(document, start, end).Select(paragraph =>
        {
            var from = paragraph.Id == start.ParagraphId ? Math.Clamp(start.Offset, 0, paragraph.Length) : 0;
            var to = paragraph.Id == end.ParagraphId ? Math.Clamp(end.Offset, from, paragraph.Length) : paragraph.Length;
            return paragraph.Text[from..to];
        });

    public string? Place(TextDocument document, TrackedChange change) =>
        change is Deletion { WithinInsertion: { } within } && document.Changes.Find(within) is Insertion host
            ? texts.Format("review.nested", host.Author)
            : null;

    public string Rows(IReadOnlyList<TrackedRow> rows) =>
        texts["review.row"] + ": " + Clip(rows.SelectMany(tracked => tracked.Row.Cells).SelectMany(cell => cell.Content.Blocks.OfType<ParagraphBlock>()).Select(paragraph => paragraph.Text));

    private string Clip(IEnumerable<string> pieces)
    {
        var built = new System.Text.StringBuilder();
        var first = true;
        foreach (var piece in pieces)
        {
            if (!first) built.Append(Separator);
            first = false;
            built.Append(piece, 0, Math.Min(piece.Length, Limit + 1 - Math.Min(built.Length, Limit + 1)));
            if (built.Length > Limit) return built.ToString(0, Limit) + "…";
        }
        return built.ToString();
    }
}

public sealed class FolioReviewSource : IReviewSource
{
    private readonly IActiveFolioContext _context;
    private readonly IChangeRegions _regions;
    private readonly ICommentAnchors _anchors;
    private readonly IRevisionResolver _resolver;
    private readonly ICommentEditing _comments;
    private readonly IAuthorColors _colors;
    private readonly IEditGuard _guard;
    private readonly IRevisionAuthor _author;
    private readonly IFolioReviewText _text;
    private readonly ITrackedRows _rows;

    public FolioReviewSource(IActiveFolioContext context, IChangeRegions regions, ICommentAnchors anchors, IRevisionResolver resolver, ICommentEditing comments,
        IAuthorColors colors, IEditGuard guard, IRevisionAuthor author, IFolioReviewText text, ITrackedRows rows)
    {
        _rows = rows;
        _context = context;
        _regions = regions;
        _anchors = anchors;
        _resolver = resolver;
        _comments = comments;
        _colors = colors;
        _guard = guard;
        _author = author;
        _text = text;
        context.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Changed;

    private FolioEditor? Editor => _context.Target?.Editor;

    public ReviewAbilities Abilities => Editor is { } editor
        ? new ReviewAbilities(!editor.ReadOnly() && _guard.CanResolveChanges(editor.Document), !editor.ReadOnly() && _guard.CanComment(editor.Document), true, true)
        {
            Edit = !editor.ReadOnly() && _guard.CanComment(editor.Document),
        }
        : ReviewAbilities.None;

    public IReadOnlyList<ReviewItem> Changes
    {
        get
        {
            if (Editor is not { } editor) return [];
            var document = editor.Document;
            var palette = _colors.Of(document);
            var map = _regions.Map(document);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<ReviewItem>();
            foreach (var region in map.Regions)
            {
                if (!seen.Add(region.Change.Id)) continue;
                items.Add(Item(region.Change, _text.Describe(document, region), palette) with { Place = _text.Place(document, region.Change) });
            }
            items.AddRange(document.Changes.All.Where(change => !seen.Contains(change.Id))
                .Select(change => _rows.Of(document, change.Id) is { Count: > 0 } rows ? Item(change, _text.Rows(rows), palette) : Item(change, string.Empty, palette)));
            return items;
        }
    }

    public IReadOnlyList<ReviewItem> Comments
    {
        get
        {
            if (Editor is not { } editor) return [];
            var document = editor.Document;
            var palette = _colors.Of(document);
            var items = new List<ReviewItem>();
            var anchored = _anchors.All(document);
            var anchors = new Dictionary<string, CommentAnchor>(StringComparer.Ordinal);
            foreach (var found in anchored) anchors.TryAdd(found.Comment.Id, found);
            var roots = anchored.Select(anchor => anchor.Comment).Concat(document.Comments.All.Where(comment => comment.ParentId is null)).DistinctBy(comment => comment.Id);
            foreach (var root in roots)
            {
                var anchor = anchors.GetValueOrDefault(root.Id);
                var quoted = anchor is null ? null : _text.Plain(document, anchor.Start, anchor.End);
                foreach (var (comment, depth) in Thread(document, root, 0))
                {
                    items.Add(new ReviewItem(comment.Id, ReviewItemKind.Comment, comment.Author, comment.Date, Body(comment),
                        palette.TryGetValue(comment.Author, out var color) ? color : 0xFF808080)
                    {
                        Depth = depth,
                        Resolved = comment.Resolved,
                        Note = depth == 0 && quoted is { Length: > 0 } ? "« " + (quoted.Length > 80 ? quoted[..80] + "…" : quoted) + " »" : null,
                    });
                }
            }
            return items;
        }
    }

    public string? Current
    {
        get
        {
            if (Editor is not { } editor) return null;
            var map = _regions.Map(editor.Document);
            return map.Touching(DocumentRange.At(editor.Caret)).FirstOrDefault()?.Change.Id ?? _anchors.At(editor.Document, editor.Caret)?.Comment.Id;
        }
    }

    public void Go(string id)
    {
        if (Editor is not { } editor) return;
        if (_regions.Map(editor.Document).Find(id) is { } region)
        {
            editor.Select(region.Start, region.End);
            return;
        }
        if (_rows.Of(editor.Document, id).SelectMany(tracked => tracked.Row.Cells).SelectMany(cell => cell.Content.Blocks.OfType<ParagraphBlock>()).ToList() is { Count: > 0 } paragraphs)
        {
            editor.Select(new DocumentPosition(paragraphs[0].Id, 0), new DocumentPosition(paragraphs[^1].Id, paragraphs[^1].Length));
            return;
        }
        if (_anchors.Find(editor.Document, id) is { } anchor) editor.Select(anchor.Start, anchor.End);
    }

    public void Accept(IReadOnlyList<string> ids) => Resolve(ids, RevisionVerdict.Accept);

    public void Reject(IReadOnlyList<string> ids) => Resolve(ids, RevisionVerdict.Reject);

    public void Reply(string id, string text)
    {
        if (Editor is not { } editor || editor.ReadOnly() || !_guard.CanComment(editor.Document)) return;
        var signature = _author.Sign();
        editor.Change(() => _comments.Reply(editor.Document, editor.History, id, signature.Author, signature.Date, text));
    }

    public void Edit(string id, string text)
    {
        if (Editor is not { } editor || editor.ReadOnly() || !_guard.CanComment(editor.Document)) return;
        editor.Change(() => _comments.Edit(editor.Document, editor.History, id, text));
    }

    public void ToggleResolved(string id)
    {
        if (Editor is not { } editor || editor.ReadOnly() || !_guard.CanComment(editor.Document) || editor.Document.Comments.Find(id) is not { } comment) return;
        editor.Change(() => _comments.Resolve(editor.Document, editor.History, id, !comment.Resolved));
    }

    public void Delete(IReadOnlyList<string> ids)
    {
        if (Editor is not { } editor || editor.ReadOnly() || !_guard.CanComment(editor.Document)) return;
        editor.Change(() => _comments.Delete(editor.Document, editor.History, ids));
    }

    private void Resolve(IReadOnlyList<string> ids, RevisionVerdict verdict)
    {
        if (Editor is not { } editor || editor.ReadOnly() || !_guard.CanResolveChanges(editor.Document)) return;
        var regions = _regions.Map(editor.Document).Regions;
        var open = ids.Where(id => regions.Where(region => region.Change.Id == id).All(region => !editor.RefusesRange(new DocumentRange(region.Start, region.End)))).ToList();
        if (open.Count == 0) return;
        editor.Change(() => _resolver.Resolve(editor.Document, editor.History, open, verdict));
    }

    private IEnumerable<(Comment Comment, int Depth)> Thread(TextDocument document, Comment root, int depth)
    {
        yield return (root, depth);
        if (depth > 32) yield break;
        foreach (var reply in document.Comments.RepliesTo(root.Id))
        {
            foreach (var nested in Thread(document, reply, depth + 1)) yield return nested;
        }
    }

    private string Body(Comment comment) =>
        string.Join("\n", comment.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));

    private ReviewItem Item(TrackedChange change, string text, IReadOnlyDictionary<string, uint> palette) =>
        new(change.Id, change.Kind switch
        {
            ChangeKind.Insertion => ReviewItemKind.Insertion,
            ChangeKind.Deletion => ReviewItemKind.Deletion,
            ChangeKind.Format => ReviewItemKind.Format,
            _ => ReviewItemKind.ParagraphFormat,
        }, change.Author, change.Date, text, palette.TryGetValue(change.Author, out var color) ? color : 0xFF808080)
        {
            Note = change.Comment,
        };
}
