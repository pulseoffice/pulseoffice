using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls.Review;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerReviewSource : IReviewSource
{
    private readonly IActiveSheetContext _context;
    private readonly ILedgerChangeResolution _resolution;
    private readonly PulseOffice.Foundation.Review.IAuthorPalette _palette;
    private readonly ITextCatalog _texts;
    private readonly PulseOffice.Foundation.Identity.IUserIdentity _identity;
    private readonly CommentMentions _mentions = new();

    public LedgerReviewSource(IActiveSheetContext context, ILedgerChangeResolution resolution, PulseOffice.Foundation.Review.IAuthorPalette palette, ITextCatalog texts, PulseOffice.Foundation.Identity.IUserIdentity identity)
    {
        _identity = identity;
        _context = context;
        _resolution = resolution;
        _palette = palette;
        _texts = texts;
        context.TargetChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public ReviewAbilities Abilities => _context.Workbook is { } workbook
        ? new ReviewAbilities(!workbook.Changes.IsProtected, true, true, true) { Edit = true }
        : ReviewAbilities.None;

    public IReadOnlyList<ReviewItem> Changes
    {
        get
        {
            if (_context.Workbook is not { } workbook) return [];
            var pending = workbook.Changes.Changes.Where(change => change.State == ChangeState.Pending).ToList();
            var colors = _palette.For(pending.Select(change => change.Author ?? string.Empty));
            return [.. pending.Select(change => new ReviewItem(change.Id, ReviewItemKind.Cell, change.Author ?? string.Empty, change.At,
                Describe(change.Before, change.FormulaBefore) + " → " + Describe(change.After, change.FormulaAfter), colors[change.Author ?? string.Empty])
            {
                Place = Place(workbook, change.SheetIndex, change.Address),
                Note = change.Comment,
            })];
        }
    }

    public IReadOnlyList<ReviewItem> Comments
    {
        get
        {
            if (_context.Workbook is not { } workbook) return [];
            var all = workbook.Sheets.SelectMany((sheet, index) => sheet.Comments.OrderBy(pair => pair.Key).Select(pair => (Index: index, Address: pair.Key, Comment: pair.Value))).ToList();
            var colors = _palette.For(all.SelectMany(item => new[] { item.Comment.Author }.Concat(item.Comment.Replies.Select(reply => reply.Author))).Select(author => author ?? string.Empty));
            return [.. all.SelectMany(item => new[]
            {
                new ReviewItem(Key(item.Index, item.Address), ReviewItemKind.Comment, item.Comment.Author ?? string.Empty, item.Comment.CreatedAt,
                    item.Comment.Text, colors[item.Comment.Author ?? string.Empty])
                {
                    Place = Place(workbook, item.Index, item.Address),
                    Resolved = item.Comment.IsResolved,
                },
            }.Concat(item.Comment.Replies.Select((reply, position) => new ReviewItem(
                Key(item.Index, item.Address) + "#" + position.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ReviewItemKind.Comment, reply.Author ?? string.Empty, reply.CreatedAt, reply.Text, colors[reply.Author ?? string.Empty])
            {
                Depth = 1,
                Place = Place(workbook, item.Index, item.Address),
                Resolved = item.Comment.IsResolved,
            })))];
        }
    }

    public string? Current => _context.Workbook is { } workbook
        && workbook.Sheets[_context.SheetIndex].Comments.ContainsKey(_context.Selection.Start)
            ? Key(_context.SheetIndex, _context.Selection.Start)
            : _context.Workbook?.Changes.Changes.FirstOrDefault(change => change.State == ChangeState.Pending && change.SheetIndex == _context.SheetIndex && change.Address == _context.Selection.Start)?.Id;

    public void Go(string id)
    {
        if (_context.Workbook is not { } workbook) return;
        if (workbook.Changes.Changes.FirstOrDefault(change => change.Id == id) is { } change)
        {
            Select(change.SheetIndex, change.Address);
            return;
        }
        if (Parse(Root(id)) is { } cell) Select(cell.Index, cell.Address);
    }

    public void Accept(IReadOnlyList<string> ids)
    {
        _resolution.Resolve(ids, true, _texts["review.panel.accept"]);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reject(IReadOnlyList<string> ids)
    {
        _resolution.Resolve(ids, false, _texts["review.panel.reject"]);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reply(string id, string text)
    {
        if (text.Trim() is not { Length: > 0 } trimmed) return;
        var mentions = _mentions.Detect(trimmed, People());
        Change(id, "command.ledger.comment.edit.title", (comment, _) =>
            comment with { Replies = [.. comment.Replies, new CommentReply { Text = trimmed, Author = _identity.Name, Mentions = mentions }] });
    }

    public void Edit(string id, string text) => Change(id, "command.ledger.comment.edit.title", (comment, reply) => reply is { } position
        ? position < comment.Replies.Count && !string.Equals(comment.Replies[position].Text, text, StringComparison.Ordinal)
            ? comment with { Replies = [.. comment.Replies.Select((item, index) => index == position ? item with { Text = text, Mentions = _mentions.Detect(text, People()) } : item)] }
            : comment
        : string.Equals(comment.Text, text, StringComparison.Ordinal) ? comment : comment with { Text = text, Mentions = _mentions.Detect(text, People()) });

    private IEnumerable<string> People() => _context.Workbook is { } workbook
        ? workbook.Sheets.SelectMany(sheet => sheet.Comments.Values)
            .SelectMany(comment => new[] { comment.Author }.Concat(comment.Replies.Select(reply => reply.Author)))
            .OfType<string>()
            .Append(_identity.Name)
        : [_identity.Name];

    public void ToggleResolved(string id) =>
        Change(id, "history.comment.resolve", (comment, _) => comment with { IsResolved = !comment.IsResolved });

    private void Change(string id, string titleKey, Func<CellComment, int?, CellComment> change)
    {
        if (_context.Workbook is not { } workbook || Parse(Root(id)) is not { } cell) return;
        if (cell.Index >= workbook.Sheets.Count) return;
        var sheet = workbook.Sheets[cell.Index];
        if (sheet.Protection.IsEnabled || !sheet.Comments.TryGetValue(cell.Address, out var before)) return;
        var after = change(before, ReplyIndex(id));
        if (ReferenceEquals(after, before)) return;
        void Set(CellComment comment)
        {
            sheet.Comments[cell.Address] = comment;
            _context.RequestRefresh();
        }
        Set(after);
        _context.History?.Push(new DelegateAction(_texts[titleKey], redo: () => Set(after), undo: () => Set(before)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string Root(string id) => id.Split('#', 2)[0];

    private int? ReplyIndex(string id) =>
        id.Split('#', 2) is [_, var position] && int.TryParse(position, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) ? index : null;

    public void Delete(IReadOnlyList<string> ids)
    {
        if (_context.Workbook is not { } workbook) return;
        foreach (var reply in ids.Where(id => ReplyIndex(id) is not null && !ids.Contains(Root(id))).OrderByDescending(id => ReplyIndex(id)))
            Change(reply, "command.ledger.comment.delete.title", (comment, position) => position is { } index && index < comment.Replies.Count
                ? comment with { Replies = [.. comment.Replies.Where((_, item) => item != index)] }
                : comment);
        var removed = ids.Where(id => ReplyIndex(id) is null).Select(Parse).OfType<(int Index, CellAddress Address)>()
            .Where(cell => cell.Index < workbook.Sheets.Count && !workbook.Sheets[cell.Index].Protection.IsEnabled && workbook.Sheets[cell.Index].Comments.ContainsKey(cell.Address))
            .Select(cell => (cell.Index, cell.Address, Comment: workbook.Sheets[cell.Index].Comments[cell.Address]))
            .ToList();
        if (removed.Count == 0) return;
        void Remove()
        {
            foreach (var (index, address, _) in removed) workbook.Sheets[index].Comments.Remove(address);
            _context.RequestRefresh();
        }
        Remove();
        _context.History?.Push(new DelegateAction(_texts["command.ledger.comment.delete.title"], redo: Remove, undo: () =>
        {
            foreach (var (index, address, comment) in removed) workbook.Sheets[index].Comments[address] = comment;
            _context.RequestRefresh();
        }));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Select(int index, CellAddress address)
    {
        if (index != _context.SheetIndex) _context.SelectSheet(index);
        _context.SelectRange(CellRange.Single(address));
    }

    private string Place(Workbook workbook, int index, CellAddress address) =>
        index < workbook.Sheets.Count ? workbook.Sheets[index].Name + "." + address.ToA1() : address.ToA1();

    private string Key(int index, CellAddress address) => index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + address.ToA1();

    private (int Index, CellAddress Address)? Parse(string id)
    {
        var parts = id.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)) return null;
        return CellAddress.TryParseA1(parts[1], out var address) ? (index, address) : null;
    }

    private string Describe(Values.CellValue value, string? formula) =>
        formula is { Length: > 0 } ? formula : value.IsEmpty ? "—" : value.ToString();
}
