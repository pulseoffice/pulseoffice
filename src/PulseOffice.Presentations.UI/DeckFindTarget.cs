using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Search;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFindTarget(IActiveDeckContext context, ITextCatalog texts, ITextPatternMatcher matcher) : IFindReplaceTarget
{
    public DeckFindTarget(IActiveDeckContext context, ITextCatalog texts) : this(context, texts, new TextPatternMatcher())
    {
    }

    public const string MatchCase = "matchCase";
    public const string WholeWords = "wholeWords";
    public const string Regex = "regex";

    private readonly DeckTextSearch _search = new(new SlideTextSources(), matcher);
    private DeckTextMatch? _last;

    public IReadOnlyList<FindOption> Options { get; } = 
    [
        new(MatchCase, "find.matchCase"),
        new(WholeWords, "folio.find.wholeWords"),
        new(Regex, "find.regex"),
    ];

    public TextPattern PatternOf(FindRequest request) =>
        new(request.Query, request.Has(MatchCase), request.Has(WholeWords), request.Has(Regex));

    public string InitialQuery => string.Empty;

    public bool IsReadOnly => context.Target?.Actions.IsReadOnly == true;

    public bool SupportsFindNext => true;

    public string? Summarize(FindRequest request)
    {
        if (context.Target is not { } target || request.Query.Length == 0) return null;
        var count = _search.Matches(target.Deck.Slides, PatternOf(request)).Count;
        return count switch
        {
            0 => texts["find.matches.none"],
            1 => texts["find.matches.one"],
            _ => texts.Format("find.matches.many", count),
        };
    }

    public string FindNext(FindRequest request)
    {
        if (context.Target is not { } target) return string.Empty;
        var matches = _search.Matches(target.Deck.Slides, PatternOf(request));
        if (_search.Next(matches, _last, target.CurrentIndex()) is not { } match)
        {
            _last = null;
            return texts["find.matches.none"];
        }

        _last = match;
        target.GoToSlide(match.SlideIndex);
        if (match.ElementId is { } id && target.Deck.Slides[match.SlideIndex].Find(id) is { } element)
        {
            target.Select([element]);
            if (match.InCell)
                target.Options.SetTableCells((id, new TableCellRange(match.Row, match.Column, match.Row, match.Column)));
        }
        else if (match.InNotes)
            target.SelectNotes(match.Offset, match.Length);
        return texts.Format(match.InNotes ? "find.foundInNotes" : "find.foundOnSlide", match.SlideIndex + 1);
    }

    public string ReplaceAll(FindRequest request)
    {
        if (context.Target is not { } target) return string.Empty;
        if (target.Actions.IsReadOnly) return texts["status.readOnly"];

        var pattern = PatternOf(request);
        var total = 0;
        var changed = target.Actions.RewriteTexts("find.replaceAll", text =>
        {
            var (updated, count) = _search.Replace(text, pattern, request.Replacement);
            total += count;
            return count == 0 ? text : updated;
        }, notes => ReplaceNotes(notes, pattern, request.Replacement, ref total));

        return texts.Format("find.replacedText", changed == 0 ? 0 : total);
    }

    private string ReplaceNotes(string notes, TextPattern pattern, string replacement, ref int total)
    {
        var hits = matcher.Find(pattern, notes ?? string.Empty, replacement);
        if (hits.Count == 0) return notes ?? string.Empty;
        total += hits.Count;
        var rebuilt = notes!;
        for (var index = hits.Count - 1; index >= 0; index--)
            rebuilt = rebuilt[..hits[index].Start] + hits[index].Replacement + rebuilt[(hits[index].Start + hits[index].Length)..];
        return rebuilt;
    }
}
