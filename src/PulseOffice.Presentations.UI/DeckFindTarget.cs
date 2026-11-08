using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Search;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFindTarget(IActiveDeckContext context, ITextCatalog texts) : IFindReplaceTarget
{
    private const string MatchCase = "matchCase";

    private readonly DeckTextSearch _search = new();
    private DeckTextMatch? _last;

    public IReadOnlyList<FindOption> Options { get; } = [new(MatchCase, "find.matchCase")];

    public string InitialQuery => string.Empty;

    public bool IsReadOnly => context.Target?.Actions.IsReadOnly == true;

    public bool SupportsFindNext => true;

    public string? Summarize(FindRequest request) => null;

    public string FindNext(FindRequest request)
    {
        if (context.Target is not { } target) return string.Empty;
        var matches = _search.Matches(target.Deck.Slides, request.Query, request.Has(MatchCase));
        if (_search.Next(matches, _last, target.CurrentIndex()) is not { } match)
        {
            _last = null;
            return texts["find.matches.none"];
        }

        _last = match;
        target.GoToSlide(match.SlideIndex);
        if (match.ElementId is { } id && target.Deck.Slides[match.SlideIndex].Find(id) is { } element)
            target.Select([element]);
        return texts.Format(match.InNotes ? "find.foundInNotes" : "find.foundOnSlide", match.SlideIndex + 1);
    }

    public string ReplaceAll(FindRequest request)
    {
        if (context.Target is not { } target) return string.Empty;
        if (target.Actions.IsReadOnly) return texts["status.readOnly"];

        var matchCase = request.Has(MatchCase);
        var total = 0;
        var changed = target.Actions.RewriteTexts("find.replaceAll", text =>
        {
            var (updated, count) = _search.Replace(text, request.Query, request.Replacement, matchCase);
            total += count;
            return count == 0 ? text : updated;
        });

        return texts.Format("find.replacedText", changed == 0 ? 0 : total);
    }
}
