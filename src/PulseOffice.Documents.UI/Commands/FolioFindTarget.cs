using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Search;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioFindTarget(
    FolioEditor editor, FolioFindCommands commands, ITextCatalog texts, string initialQuery, SearchQuery? previous, bool withFormats = true) : IFindReplaceTarget
{
    private const string MatchCase = "matchCase";
    private const string WholeWords = "wholeWords";
    private const string Regex = "regex";
    private const string Wildcards = "wildcards";
    private const string Find = "find";
    private const string Replacement = "replace";
    private const string ParagraphStyle = "ParagraphStyle";
    private const string CharacterStyle = "CharacterStyle";
    private const string Emphasis = "Emphasis";

    public IReadOnlyList<FindOption> Options { get; } =
    [
        new(MatchCase, "find.matchCase", previous?.MatchCase ?? false),
        new(WholeWords, "folio.find.wholeWords", previous?.WholeWords ?? false),
        new(Regex, "find.regex", previous?.Regex ?? false),
        new(Wildcards, "folio.find.wildcards", previous?.Wildcards ?? false),
    ];

    public IReadOnlyList<FindChoice> Choices => withFormats ? [.. ChoicesFor(Find), .. ChoicesFor(Replacement)] : [];

    public string InitialQuery => initialQuery;

    public bool IsReadOnly => editor.ReadOnly();

    public bool SupportsFindNext => true;

    public bool SupportsFindPrevious => true;

    public string? Summarize(FindRequest request)
    {
        var query = QueryOf(request);
        if (query.IsEmpty) return string.Empty;
        if (!commands.IsValid(query)) return texts["folio.find.invalidPattern"];
        return commands.Count(editor, query) switch
        {
            0 => texts["find.matches.none"],
            1 => texts["find.matches.one"],
            var many => texts.Format("find.matches.many", many),
        };
    }

    public string FindNext(FindRequest request) => commands.Next(editor, QueryOf(request)) ? string.Empty : NotFound(request);

    public string FindPrevious(FindRequest request) => commands.Next(editor, QueryOf(request), backwards: true) ? string.Empty : NotFound(request);

    public bool SupportsReplace => true;

    public string Replace(FindRequest request) =>
        commands.Replace(editor, QueryOf(request), request.Replacement, FormatOf(request, Replacement)) ? string.Empty : NotFound(request);

    public string ReplaceAll(FindRequest request) =>
        texts.Format("folio.find.replaced", commands.ReplaceAll(editor, QueryOf(request), request.Replacement, FormatOf(request, Replacement)));

    public SearchQuery QueryOf(FindRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.Query, request.Has(MatchCase), request.Has(WholeWords), request.Has(Regex))
        {
            Wildcards = request.Has(Wildcards),
            Format = FormatOf(request, Find),
        };
    }

    private string NotFound(FindRequest request) => texts.Format("folio.find.notFound", request.Query);

    private SearchFormat? FormatOf(FindRequest request, string side)
    {
        var emphasis = request.Choice(side + Emphasis);
        var format = new SearchFormat(
            request.Choice(side + ParagraphStyle),
            request.Choice(side + CharacterStyle),
            emphasis switch { "bold" => true, "notBold" => false, _ => null },
            emphasis switch { "italic" => true, "notItalic" => false, _ => null },
            emphasis switch { "underline" => true, "notUnderline" => false, _ => null });
        return format.IsEmpty ? null : format;
    }

    private IEnumerable<FindChoice> ChoicesFor(string side)
    {
        var replacing = side == Replacement;
        var none = new FindChoiceValue(string.Empty, texts[replacing ? "find.format.keep" : "find.format.any"]);
        var styles = editor.Document.Styles;
        yield return new FindChoice(side + ParagraphStyle, "find.format.paragraphStyle",
            [none, .. styles.Paragraph.All.Select(style => new FindChoiceValue(style.Name, style.Name))], replacing);
        yield return new FindChoice(side + CharacterStyle, "find.format.characterStyle",
            [none, .. styles.Character.All.Select(style => new FindChoiceValue(style.Name, style.Name))], replacing);
        yield return new FindChoice(side + Emphasis, "find.format.emphasis",
            [none, .. new[] { "bold", "notBold", "italic", "notItalic", "underline", "notUnderline" }.Select(key => new FindChoiceValue(key, texts["find.format." + key]))], replacing);
    }
}
