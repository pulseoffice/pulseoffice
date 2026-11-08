using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Annotations;

public sealed class Comment(string id, string author, DateTimeOffset date, BlockContainer content)
{
    public string Id { get; } = id;

    public string Author { get; set; } = author;

    public string? Initials { get; set; }

    public DateTimeOffset Date { get; set; } = date;

    public BlockContainer Content { get; } = content;

    public string? ParentId { get; set; }

    public bool Resolved { get; set; }

    public bool IsReply => ParentId is not null;

    public Comment Clone() => new(Id, Author, Date, Content.Clone())
    {
        Initials = Initials,
        ParentId = ParentId,
        Resolved = Resolved,
    };
}

public sealed class CommentCollection
{
    private readonly List<Comment> _comments = [];

    private Dictionary<string, Comment>? _byId;

    public void Replace(IEnumerable<Comment> items)
    {
        var copy = items.ToList();
        _comments.Clear();
        _comments.AddRange(copy);
        _byId = null;
    }

    public IReadOnlyList<Comment> All => _comments;

    public Comment? Find(string id)
    {
        if (_byId is null)
        {
            _byId = new Dictionary<string, Comment>(StringComparer.Ordinal);
            foreach (var comment in _comments) _byId.TryAdd(comment.Id, comment);
        }
        return _byId.GetValueOrDefault(id);
    }

    public void Add(Comment comment)
    {
        _comments.Add(comment);
        _byId = null;
    }

    public bool Remove(string id)
    {
        _byId = null;
        return _comments.RemoveAll(comment => comment.Id == id) > 0;
    }

    public IEnumerable<Comment> RepliesTo(string id) => _comments.Where(comment => comment.ParentId == id);

    public IEnumerable<Comment> Thread(string id)
    {
        if (Find(id) is not { } root) return [];
        var replies = _comments.Where(comment => comment.ParentId is not null).ToLookup(comment => comment.ParentId!, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { root.Id };
        var thread = new List<Comment>();
        var pending = new Stack<Comment>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            thread.Add(current);
            foreach (var reply in replies[current.Id].Reverse())
                if (seen.Add(reply.Id)) pending.Push(reply);
        }
        return thread;
    }

    public int DetachBrokenReplies()
    {
        var detached = 0;
        var order = _comments.Select((comment, index) => (comment.Id, index)).GroupBy(pair => pair.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);
        foreach (var comment in _comments)
        {
            var path = new List<Comment>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = comment;
            while (current.ParentId is { } parent)
            {
                path.Add(current);
                seen.Add(current.Id);
                if (Find(parent) is not { } above || ReferenceEquals(above, current))
                {
                    current.ParentId = null;
                    detached++;
                    break;
                }
                if (seen.Contains(above.Id))
                {
                    var loop = path.SkipWhile(member => member.Id != above.Id).ToList();
                    loop.MinBy(member => order[member.Id])!.ParentId = null;
                    detached++;
                    break;
                }
                current = above;
            }
        }
        return detached;
    }

    public void CopyFrom(CommentCollection other)
    {
        _comments.Clear();
        _comments.AddRange(other._comments.Select(comment => comment.Clone()));
        _byId = null;
    }
}
