namespace PulseOffice.UI.Controls.Review;

public enum ReviewItemKind
{
    Insertion,
    Deletion,
    Format,
    ParagraphFormat,
    Cell,
    Comment,
}

public sealed record ReviewItem(string Id, ReviewItemKind Kind, string Author, DateTimeOffset Date, string Text, uint Color)
{
    public int Depth { get; init; }

    public bool Resolved { get; init; }

    public string? Note { get; init; }

    public string? Place { get; init; }
}

public sealed record ReviewAbilities(bool Resolve, bool Comment, bool Replies, bool ResolveComments)
{
    public static readonly ReviewAbilities None = new(false, false, false, false);

    public bool Edit { get; init; }
}

public interface IReviewSource
{
    ReviewAbilities Abilities { get; }

    IReadOnlyList<ReviewItem> Changes { get; }

    IReadOnlyList<ReviewItem> Comments { get; }

    string? Current { get; }

    void Go(string id);

    void Accept(IReadOnlyList<string> ids);

    void Reject(IReadOnlyList<string> ids);

    void Reply(string id, string text);

    void Edit(string id, string text);

    void ToggleResolved(string id);

    void Delete(IReadOnlyList<string> ids);

    event EventHandler? Changed;
}
