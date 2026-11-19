using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record OutlineEntry(string Title, IReadOnlyList<OutlineLine> Body);

public interface IDeckOutlineText
{
    string Render(IReadOnlyList<Slide> slides);

    IReadOnlyList<OutlineEntry> Parse(string text);
}

public sealed class DeckOutlineText : IDeckOutlineText
{
    private const int MaxLevel = 8;
    private const string SpaceIndent = "    ";
    private const char TitleMark = '\u00B6';
    private const string TitleSeparator = " \u00B6 ";
    private const string EscapedMark = "\u00B6\u00B6";

    public string Render(IReadOnlyList<Slide> slides)
    {
        ArgumentNullException.ThrowIfNull(slides);
        var lines = new List<string>();
        foreach (var slide in slides)
        {
            lines.Add(TitleLine(slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText ?? string.Empty));
            if (slide.Placeholder(PlaceholderRole.Body)?.Text is not { IsEmpty: false } body) continue;
            foreach (var paragraph in body.Paragraphs)
                lines.Add(new string('\t', Math.Clamp(paragraph.BulletLevel, 0, MaxLevel) + 1) + paragraph.Text);
        }
        return string.Join(Environment.NewLine, lines);
    }

    public IReadOnlyList<OutlineEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<OutlineEntry>();
        string? title = null;
        var body = new List<OutlineLine>();

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var (depth, content) = Indent(raw);
            if (depth == 0)
            {
                if (title is not null) entries.Add(new OutlineEntry(title, body));
                title = TitleParagraphs(content);
                body = [];
            }
            else
            {
                title ??= string.Empty;
                body.Add(new OutlineLine(Math.Min(depth - 1, MaxLevel), content));
            }
        }

        if (title is not null) entries.Add(new OutlineEntry(title, body));
        return entries;
    }

    private string TitleLine(string title) =>
        title.Replace(TitleMark.ToString(), EscapedMark, StringComparison.Ordinal).Replace("\n", TitleSeparator, StringComparison.Ordinal);

    private string TitleParagraphs(string content)
    {
        if (!content.Contains(TitleMark, StringComparison.Ordinal)) return content;
        var paragraphs = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var at = 0; at < content.Length; at++)
        {
            if (content[at] != TitleMark)
            {
                current.Append(content[at]);
                continue;
            }
            if (at + 1 < content.Length && content[at + 1] == TitleMark)
            {
                current.Append(TitleMark);
                at++;
                continue;
            }
            paragraphs.Add(current.ToString().Trim(' '));
            current.Clear();
        }
        paragraphs.Add(current.ToString().Trim(' '));
        return string.Join("\n", paragraphs);
    }

    private (int Depth, string Content) Indent(string line)
    {
        var depth = 0;
        var at = 0;
        while (at < line.Length)
        {
            if (line[at] == '\t') { depth++; at++; }
            else if (string.CompareOrdinal(line, at, SpaceIndent, 0, SpaceIndent.Length) == 0) { depth++; at += SpaceIndent.Length; }
            else break;
        }
        return (depth, line[at..].TrimStart(' '));
    }
}
