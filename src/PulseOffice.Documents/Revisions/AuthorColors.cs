using PulseOffice.Foundation.Review;

namespace PulseOffice.Documents.Revisions;

public interface IAuthorColors
{
    IReadOnlyDictionary<string, uint> Of(TextDocument document);
}

public sealed class AuthorColors(IAuthorPalette palette) : IAuthorColors
{
    public IReadOnlyDictionary<string, uint> Of(TextDocument document) =>
        palette.For(document.Changes.All.Select(change => change.Author).Concat(document.Comments.All.Select(comment => comment.Author)));
}
