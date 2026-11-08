using System.Runtime.CompilerServices;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;

namespace PulseOffice.Documents.UI.Editing;

public sealed record ProofMark(int Start, int Length, bool Grammar, string Language)
{
    public string? Rule { get; init; }
}

public sealed record LanguageSegment(int Start, int End, string? Language);

public interface IFolioLanguages
{
    IReadOnlyList<LanguageSegment> Segments(TextDocument document, TextParagraph paragraph);

    string? At(TextDocument document, TextParagraph paragraph, int offset);
}

public sealed class FolioLanguages(IStyleResolver styles) : IFolioLanguages
{
    public const string NoProofing = "zxx";

    private readonly ConditionalWeakTable<TextParagraph, IReadOnlyList<LanguageSegment>> _cache = new();

    public IReadOnlyList<LanguageSegment> Segments(TextDocument document, TextParagraph paragraph) =>
        _cache.GetValue(paragraph, content => Build(document, content));

    public string? At(TextDocument document, TextParagraph paragraph, int offset) =>
        Segments(document, paragraph).LastOrDefault(segment => segment.Start <= offset)?.Language ?? document.Language;

    private IReadOnlyList<LanguageSegment> Build(TextDocument document, TextParagraph content)
    {
        var runs = content.Runs.Count == 0 ? [new TextRun(content.Text)] : content.Runs;
        var segments = new List<LanguageSegment>();
        var position = 0;
        foreach (var run in runs)
        {
            var language = styles.EffectiveRun(document.Styles, content, run).Language ?? document.Language;
            var end = position + run.Text.Length;
            if (segments.Count > 0 && segments[^1].Language == language) segments[^1] = segments[^1] with { End = end };
            else segments.Add(new LanguageSegment(position, end, language));
            position = end;
        }
        return segments;
    }
}

public interface IFolioProofing
{
    bool Enabled { get; }

    IReadOnlyList<WordSpan> Mistakes(string text, string? language);

    IReadOnlyList<ProofMark> Marks(TextDocument document, TextParagraph paragraph, bool always = false);

    IReadOnlyList<string> Suggestions(ProofMark mark, string text);
}

public sealed class FolioProofing(ISpellChecker checker, IGrammarChecker grammar, ITextLanguageResolver languages, IAutoSpellSettings settings, IUserDictionary words,
    PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges dictionaries, IFolioLanguages segments) : IFolioProofing
{
    private const int Capacity = 2048;

    private readonly Dictionary<(string Text, string Language), IReadOnlyList<WordSpan>> _cache = [];
    private readonly Dictionary<(string Text, string Language), IReadOnlyList<GrammarIssue>> _grammar = [];
    private bool _listening;

    public bool Enabled => settings.Enabled;

    public IReadOnlyList<WordSpan> Mistakes(string text, string? language)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        Listen();
        var tag = languages.Resolve(language);
        if (!checker.HasDictionary(tag)) return [];
        var key = (text, tag.Canonical);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        if (_cache.Count >= Capacity) _cache.Clear();
        IReadOnlyList<WordSpan> found = [.. checker.Check(text, tag, 0).Select(mistake => mistake.Word)];
        _cache[key] = found;
        return found;
    }

    public IReadOnlyList<string> Suggestions(ProofMark mark, string text)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(text);
        if (mark.Start < 0 || mark.Length <= 0 || mark.Start >= text.Length) return [];
        var tag = languages.Resolve(mark.Language);
        var word = text.Substring(mark.Start, Math.Min(mark.Length, text.Length - mark.Start));
        if (!mark.Grammar) return checker.Check(word, tag).FirstOrDefault()?.Suggestions ?? [];
        return grammar.Check(text, tag).FirstOrDefault(issue => issue.Span.Start == mark.Start)?.Suggestions ?? [];
    }

    public IReadOnlyList<ProofMark> Marks(TextDocument document, TextParagraph paragraph, bool always = false)
    {
        if (!settings.Enabled && !always || string.IsNullOrWhiteSpace(paragraph.Text)) return [];
        var marks = new List<ProofMark>();
        var text = paragraph.Text;
        foreach (var segment in segments.Segments(document, paragraph))
        {
            if (segment.Language == FolioLanguages.NoProofing || segment.End <= segment.Start) continue;
            var piece = text[segment.Start..Math.Min(segment.End, text.Length)];
            var language = segment.Language ?? document.Language;
            marks.AddRange(Mistakes(piece, language).Select(word => new ProofMark(segment.Start + word.Start, word.Length, false, language ?? string.Empty)));
            marks.AddRange(Grammar(piece, language).Select(issue => new ProofMark(segment.Start + issue.Span.Start, issue.Span.Length, true, language ?? string.Empty) { Rule = issue.Rule }));
        }
        return marks;
    }

    private IReadOnlyList<GrammarIssue> Grammar(string text, string? language)
    {
        var tag = languages.Resolve(language);
        var key = (text, tag.Canonical);
        if (_grammar.TryGetValue(key, out var cached)) return cached;
        if (_grammar.Count >= Capacity) _grammar.Clear();
        var found = grammar.Check(text, tag);
        _grammar[key] = found;
        return found;
    }

    private void Listen()
    {
        if (_listening) return;
        words.Changed += (_, _) => _cache.Clear();
        dictionaries.Changed += (_, _) => _cache.Clear();
        _listening = true;
    }
}
