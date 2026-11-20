using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record SearchFormat(string? ParagraphStyle = null, string? CharacterStyle = null, bool? Bold = null, bool? Italic = null, bool? Underline = null)
{
    public bool IsEmpty => ParagraphStyle is null && CharacterStyle is null && Bold is null && Italic is null && Underline is null;

    public bool HasCharacterFormat => Bold is not null || Italic is not null || Underline is not null;
}

public sealed record SearchQuery(string Text, bool MatchCase = false, bool WholeWords = false, bool Regex = false)
{
    public bool Wildcards { get; init; }

    public SearchFormat? Format { get; init; }

    public bool HasFormat => Format is { IsEmpty: false };

    public bool IsEmpty => Text.Length == 0 && !HasFormat;
}

public sealed record SearchStory(IReadOnlyList<ParagraphBlock> Paragraphs)
{
    public string? CommentId { get; init; }

    public PulseOffice.Documents.Styles.StyleSheet? Styles { get; init; }

    public IReadOnlySet<string> SectionStarts { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

public enum SearchBreak
{
    Page,
    Column,
    Section,
}

public sealed record SearchBreakAt(string ParagraphId, SearchBreak Kind);

public sealed record SearchMatch(DocumentRange Range, string Found)
{
    public IReadOnlyList<SearchBreakAt> Breaks { get; init; } = [];

    public string? CommentId { get; init; }

    public System.Text.RegularExpressions.Match? Pattern { get; init; }
}

public static class SearchMarks
{
    public const char Paragraph = '\n';

    public const char Page = '\f';

    public const char Column = '\u000E';

    public const char Section = '\u001D';

    public const char Line = '\u2028';

    public static bool IsBreak(char character) => character is Paragraph or Page or Column or Section;
}
