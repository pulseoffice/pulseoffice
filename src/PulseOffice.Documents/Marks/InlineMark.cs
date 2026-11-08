namespace PulseOffice.Documents.Marks;

public enum MarkEdge
{
    Point,
    Start,
    End,
}

public abstract record InlineMark(int Offset)
{
    public abstract string Key { get; }

    public virtual bool StaysAtEnd => false;

    public virtual bool OpensRange => false;

    public virtual bool FollowsPrecedingText => false;

    public virtual MarkEdge Span => MarkEdge.Point;

    public virtual bool InsideRange => false;

    public InlineMark At(int offset) => this with { Offset = offset };
}

public sealed record BookmarkMark(int Offset, string Name, MarkEdge Edge) : InlineMark(Offset)
{
    public override MarkEdge Span => Edge;

    public override string Key => "bookmark:" + Name;

    public override bool StaysAtEnd => Edge == MarkEdge.End;

    public override bool OpensRange => Edge == MarkEdge.Start;
}

public sealed record CommentMark(int Offset, string CommentId, MarkEdge Edge) : InlineMark(Offset)
{
    public override MarkEdge Span => Edge;

    public override string Key => "comment:" + CommentId;

    public override bool StaysAtEnd => Edge == MarkEdge.End;

    public override bool OpensRange => Edge == MarkEdge.Start;
}

public sealed record ChangeMark(int Offset, string ChangeId, MarkEdge Edge) : InlineMark(Offset)
{
    public override MarkEdge Span => Edge;

    public override string Key => "change:" + ChangeId;

    public override bool StaysAtEnd => Edge == MarkEdge.End;

    public override bool OpensRange => Edge == MarkEdge.Start;
}

public sealed record NoteMark(int Offset, string NoteId) : InlineMark(Offset)
{
    public override string Key => "note:" + NoteId;

    public override bool FollowsPrecedingText => Offset > 0;
}

public sealed record ObjectMark(int Offset, string ObjectId) : InlineMark(Offset)
{
    public override string Key => "object:" + ObjectId;
}

public sealed record ContentControlMark(int Offset, string ControlId, MarkEdge Edge) : InlineMark(Offset)
{
    public Controls.ContentControl? Control { get; init; }

    public override MarkEdge Span => Edge;

    public override string Key => "control:" + ControlId;

    public override bool StaysAtEnd => Edge == MarkEdge.End;

    public override bool OpensRange => Edge == MarkEdge.Start;
}

public sealed record IndexEntryMark(int Offset, string Entry, MarkEdge Edge) : InlineMark(Offset)
{
    public override MarkEdge Span => Edge;

    public string Index { get; init; } = "alphabetical";

    public string? Key1 { get; init; }

    public string? Key2 { get; init; }

    public IReadOnlyList<string> Path => [.. new[] { Key1, Key2 }.OfType<string>(), Entry];

    public override string Key => "index:" + Index + ":" + string.Join("\u001F", Path);

    public override bool OpensRange => Edge == MarkEdge.Start;
}

public enum FieldCodePart
{
    Begin,
    Code,
    Separate,
    End,
}

public sealed record FieldCodeMark(int Offset, string FieldId, FieldCodePart Part, string Code) : InlineMark(Offset)
{
    public const char NestedBegin = '';

    public const char NestedSeparate = '';

    public const char NestedEnd = '';

    public const int DataLimit = 131072;

    public string? Data
    {
        get;
        init => field = value is { Length: > 0 and <= DataLimit } && System.Buffers.Text.Base64.IsValid(value) ? value : null;
    }

    public bool Locked { get; init; }

    public override MarkEdge Span => Part switch
    {
        FieldCodePart.Begin => MarkEdge.Start,
        FieldCodePart.End => MarkEdge.End,
        _ => MarkEdge.Point,
    };

    public override bool InsideRange => Part is FieldCodePart.Code or FieldCodePart.Separate;

    public override string Key => "field:" + FieldId;

    public override bool StaysAtEnd => Part == FieldCodePart.End;

    public override bool OpensRange => Part == FieldCodePart.Begin;
}
