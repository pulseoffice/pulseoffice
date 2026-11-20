using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public static class ReferenceFormats
{
    public const string Text = "text";
    public const string Page = "page";
    public const string Number = "number";
    public const string NumberNoContext = "number-no-context";
    public const string NumberFullContext = "number-all-superior";
    public const string Direction = "direction";
    public const string Chapter = "chapter";
    public const string Caption = "caption";
    public const string Category = "category-and-value";
    public const string Value = "value";
}

public interface IFieldLayoutFacts
{
    string? PageOf(string paragraphId);

    string? NoteCitation(string noteId);

    string? ListLabel(string paragraphId);

    int? PageCount { get; }
}

public sealed class NoFieldLayoutFacts : IFieldLayoutFacts
{
    public string? PageOf(string paragraphId) => null;

    public string? NoteCitation(string noteId) => null;

    public string? ListLabel(string paragraphId) => null;

    public int? PageCount => null;
}

public interface IFieldWords
{
    string Above { get; }

    string Below { get; }

    string MissingReference { get; }
}

public sealed class InvariantFieldWords : IFieldWords
{
    public string Above => "above";

    public string Below => "below";

    public string MissingReference => "Error: Reference source not found";
}

public sealed record FieldValue(string Text, bool? Hidden = null);

public sealed record FieldUpdate(IFieldLayoutFacts Facts, DateTime Now)
{
    public IReadOnlySet<TextFieldKind>? Kinds { get; init; }

    public IReadOnlySet<string>? Paragraphs { get; init; }
}

public sealed class FieldContext(TextDocument document, FieldUpdate request, IReadOnlyDictionary<string, int> order)
{
    public TextDocument Document { get; } = document;

    public FieldUpdate Request { get; } = request;

    public IFieldLayoutFacts Facts => Request.Facts;

    public Dictionary<string, string> Variables { get; } = new(document.Variables, StringComparer.Ordinal);

    public Dictionary<string, int> Sequences { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ParagraphBlock Paragraph { get; set; } = null!;

    public int Offset { get; set; }

    private readonly OutlineEntry?[] _chapters = new OutlineEntry?[ChapterLevels.Deepest + 1];

    public OutlineEntry? Chapter => ChapterAt(1);

    public void Enter(OutlineEntry heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        var level = Math.Clamp(heading.Level, 1, ChapterLevels.Deepest);
        _chapters[level] = heading;
        for (var deeper = level + 1; deeper < _chapters.Length; deeper++) _chapters[deeper] = null;
    }

    public OutlineEntry? ChapterAt(int level)
    {
        for (var current = Math.Clamp(level, 1, ChapterLevels.Deepest); current >= 1; current--)
            if (_chapters[current] is { } heading) return heading;
        return null;
    }

    public Editing.DocumentStatistics? Statistics { get; set; }

    public FieldObjectCounts? ObjectCounts { get; set; }

    public int OrderOf(string paragraphId) => order.GetValueOrDefault(paragraphId, -1);

    public int CurrentOrder => OrderOf(Paragraph.Id);
}

public static class ChapterLevels
{
    public const int Deepest = 10;
}

public sealed record FieldObjectCounts(int Tables, int Images, int Objects);
