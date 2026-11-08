using PulseOffice.Foundation.Review;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Review;

namespace PulseOffice.Presentations.UI;

public sealed class DeckReviewSource : IReviewSource
{
    private readonly IActiveDeckContext _context;
    private readonly IAuthorPalette _palette;
    private readonly ITextCatalog _texts;

    public DeckReviewSource(IActiveDeckContext context, IAuthorPalette palette, ITextCatalog texts)
    {
        _context = context;
        _palette = palette;
        _texts = texts;
        context.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public ReviewAbilities Abilities => _context.Target is null ? ReviewAbilities.None : new ReviewAbilities(false, true, false, false) { Edit = true };

    public IReadOnlyList<ReviewItem> Changes => [];

    public IReadOnlyList<ReviewItem> Comments
    {
        get
        {
            if (_context.Target is not { } target) return [];
            var all = target.Deck.Slides.SelectMany((slide, index) => slide.Comments.Select(comment => (Index: index, Comment: comment))).ToList();
            var colors = _palette.For(all.Select(item => item.Comment.Author));
            return [.. all.Select(item => new ReviewItem(item.Comment.Id, ReviewItemKind.Comment, item.Comment.Author, item.Comment.CreatedAt, item.Comment.Text, colors[item.Comment.Author])
            {
                Place = _texts.Format("deck.review.slide", item.Index + 1),
            })];
        }
    }

    public string? Current => _context.Target is { } target && target.CurrentSlide() is { } slide ? slide.Comments.FirstOrDefault()?.Id : null;

    public void Go(string id)
    {
        if (_context.Target is not { } target) return;
        for (var index = 0; index < target.Deck.Slides.Count; index++)
        {
            if (target.Deck.Slides[index].Comments.Any(comment => comment.Id == id)) target.GoToSlide(index);
        }
    }

    public void Accept(IReadOnlyList<string> ids)
    {
    }

    public void Reject(IReadOnlyList<string> ids)
    {
    }

    public void Reply(string id, string text)
    {
    }

    public void Edit(string id, string text)
    {
        if (_context.Target is not { } target) return;
        foreach (var slide in target.Deck.Slides)
        {
            if (slide.Comments.FirstOrDefault(comment => comment.Id == id) is not { } comment) continue;
            target.Actions.EditComment(slide, comment, text);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
    }

    public void ToggleResolved(string id)
    {
    }

    public void Delete(IReadOnlyList<string> ids)
    {
        if (_context.Target is not { } target) return;
        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        var removed = target.Deck.Slides.SelectMany(slide => slide.Comments.Where(comment => wanted.Contains(comment.Id)).Select(comment => (slide, comment))).ToList();
        target.Actions.RemoveComments(removed);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
