using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed record CellComment
{
    public required string Text { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public bool IsAlwaysVisible { get; init; }

    public IReadOnlyList<CommentReply> Replies { get; init; } = [];

    public bool IsResolved { get; init; }

    public CommentBox? Box { get; init; }

    public CommentTextSpan? AnnotatedText { get; init; }

    public IReadOnlyList<CommentMention> Mentions { get; init; } = [];

    public bool IsThread => Replies.Count > 0 || IsResolved || Mentions.Count > 0;

    public IEnumerable<string> Texts => new[] { Text }.Concat(Replies.Select(reply => reply.Text));
}

public sealed record CommentReply
{
    public required string Text { get; init; }

    public string? Author { get; init; }

    public IReadOnlyList<CommentMention> Mentions { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

public sealed record CommentBox(double OffsetX, double OffsetY, double Width, double Height)
{
    public static CommentBox Default { get; } = new(10, -7, 108, 60);
}

public sealed record CommentTextSpan(int Start, int Length);

public sealed record CommentMention(int Start, int Length, string Person);

public sealed record OutlineSummary(bool Below = true, bool Right = true)
{
    public static OutlineSummary Default { get; } = new();
}

public sealed record OutlineGroup
{
    public required int First { get; init; }

    public required int Last { get; init; }

    public int Level { get; init; } = 1;

    public bool IsCollapsed { get; init; }

    public bool Contains(int index) => index >= First && index <= Last;

    public int Count => Last - First + 1;
}
