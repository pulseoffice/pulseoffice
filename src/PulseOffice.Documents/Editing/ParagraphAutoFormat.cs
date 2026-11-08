using System.Globalization;
using System.Text.RegularExpressions;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public enum AutoFormatKind { Bullets, Numbering, Border }

public sealed record AutoFormatMatch(AutoFormatKind Kind, int PrefixLength)
{
    public int? Start { get; init; }

    public BorderLine? Border { get; init; }
}

public interface IAutoFormatRule
{
    AutoFormatMatch? Match(string text);
}

public sealed partial class BulletRule : IAutoFormatRule
{
    public AutoFormatMatch? Match(string text) =>
        Prefix().Match(text) is { Success: true } found ? new AutoFormatMatch(AutoFormatKind.Bullets, found.Length) : null;

    [GeneratedRegex(@"^[-*+•–]\s+(?=\S)")]
    private partial Regex Prefix();
}

public sealed partial class NumberRule : IAutoFormatRule
{
    public AutoFormatMatch? Match(string text) =>
        Prefix().Match(text) is { Success: true } found
            ? new AutoFormatMatch(AutoFormatKind.Numbering, found.Length) { Start = int.Parse(found.Groups[1].Value, CultureInfo.InvariantCulture) }
            : null;

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+(?=\S)")]
    private partial Regex Prefix();
}

public sealed class BorderRule : IAutoFormatRule
{
    private const uint Black = 0xFF000000;

    private readonly IReadOnlyDictionary<char, BorderLine> _lines = new Dictionary<char, BorderLine>
    {
        ['-'] = new(0.75, Black),
        ['_'] = new(1.5, Black),
        ['='] = new(2.25, Black) { Style = BorderLineStyle.Double },
        ['*'] = new(4.5, Black),
        ['~'] = new(0.75, Black) { Style = BorderLineStyle.Dotted },
        ['#'] = new(3, Black) { Style = BorderLineStyle.Double },
    };

    public AutoFormatMatch? Match(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 3 || !_lines.TryGetValue(trimmed[0], out var line) || trimmed.Any(character => character != trimmed[0])) return null;
        return new AutoFormatMatch(AutoFormatKind.Border, text.Length) { Border = line };
    }
}

public interface IParagraphAutoFormat
{
    AutoFormatMatch? Recognise(string text);
}

public sealed class ParagraphAutoFormat(IEnumerable<IAutoFormatRule> rules) : IParagraphAutoFormat
{
    public AutoFormatMatch? Recognise(string text) =>
        string.IsNullOrEmpty(text) ? null : rules.Select(rule => rule.Match(text)).FirstOrDefault(match => match is not null);
}

public interface IDocumentAutoFormat
{
    AutoFormatMatch? Format(TextDocument document, Document.Commands.UndoStack history, ParagraphBlock paragraph, bool lists, bool borders);

    int FormatAll(TextDocument document, Document.Commands.UndoStack history, bool lists, bool borders);
}

public sealed class DocumentAutoFormat(IParagraphAutoFormat recogniser, IDocumentEditor editor, IListEditing listEditing, IStoryCatalog stories) : IDocumentAutoFormat
{
    public AutoFormatMatch? Format(TextDocument document, Document.Commands.UndoStack history, ParagraphBlock paragraph, bool lists, bool borders)
    {
        if (paragraph.Content.ListStyleName is not null || recogniser.Recognise(paragraph.Text) is not { } match) return null;
        if (match.Kind == AutoFormatKind.Border ? !borders : !lists) return null;
        var id = paragraph.Id;
        var remaining = paragraph.Length - match.PrefixLength;
        editor.Delete(document, history, DocumentRange.In(id, 0, match.PrefixLength));
        var whole = DocumentRange.In(id, 0, remaining);
        switch (match.Kind)
        {
            case AutoFormatKind.Bullets:
                editor.SetList(document, history, whole, Styles.DefaultStyleNames.List1, 0);
                break;
            case AutoFormatKind.Numbering:
                editor.SetList(document, history, whole, Styles.DefaultStyleNames.Numbering123, 0);
                if (match.Start is { } first and not 1) listEditing.Restart(document, history, whole, first);
                break;
            default:
                editor.SetParagraph(document, history, whole, new Styles.ParagraphProperties { Borders = new BoxBorders { Bottom = match.Border, PaddingBottom = 2 } });
                break;
        }
        return match;
    }

    public int FormatAll(TextDocument document, Document.Commands.UndoStack history, bool lists, bool borders)
    {
        var count = 0;
        using (history.BeginTransaction("autoformat"))
            foreach (var paragraph in stories.Paragraphs(document).ToList())
                if (Format(document, history, paragraph, lists, borders) is not null) count++;
        return count;
    }
}
