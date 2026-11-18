using System.Globalization;
using PulseOffice.Documents.Marks;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxIndexEntryReader
{
    public IndexEntryMark? Read(string? instruction, int offset, DocxReadContext context)
    {
        if (ContentsEntry(instruction, offset) is { } contents) return contents;
        if (EntryPath(instruction) is not { } path) return null;
        var levels = Levels(path);
        if (levels.Count == 0) return null;
        var leaf = levels.Count > 3 ? string.Join(", ", levels.Skip(2)) : levels[^1];
        var entry = new IndexEntryMark(offset, leaf, MarkEdge.Point)
        {
            Key1 = levels.Count > 1 ? levels[0] : null,
            Key2 = levels.Count > 2 ? levels[1] : null,
        };
        if (System.Text.RegularExpressions.Regex.Match(instruction!, @"(?<=\s)\\f\s+""?([^\s""\\]+)") is { Success: true } named)
            entry = entry with { Index = named.Groups[1].Value };
        if (System.Text.RegularExpressions.Regex.Match(instruction!, @"(?<=\s)\\r\s+""?([^\s""\\]+)") is { Success: true } range)
            context.IndexRanges.Add((entry, range.Groups[1].Value));
        return entry;
    }

    private IndexEntryMark? ContentsEntry(string? instruction, int offset)
    {
        var text = instruction?.Trim();
        if (text is null || !text.StartsWith("TC ", StringComparison.OrdinalIgnoreCase)) return null;
        var quoted = System.Text.RegularExpressions.Regex.Match(text, @"^TC\s+""((?:[^""\\]|\\.)*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var entry = quoted.Success
            ? quoted.Groups[1].Value.Replace("\\\"", "\"", StringComparison.Ordinal)
            : text[3..].Trim().Split(' ', 2)[0];
        if (entry.Length == 0) return null;
        var level = System.Text.RegularExpressions.Regex.Match(text, @"(?<=\s)\\l\s+""?(\d+)") is { Success: true } found
                    && int.TryParse(found.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 1, 10)
            : 1;
        return new IndexEntryMark(offset, entry, MarkEdge.Point) { Index = "toc", Level = level };
    }

    private List<string> Levels(string path)
    {
        var levels = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] == '\\' && index + 1 < path.Length && path[index + 1] == ':')
            {
                current.Append(':');
                index++;
            }
            else if (path[index] == ':')
            {
                levels.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(path[index]);
        }
        levels.Add(current.ToString().Trim());
        return [.. levels.Where(level => level.Length > 0)];
    }

    private string? EntryPath(string? instruction)
    {
        var text = instruction?.Trim();
        if (text is null || !text.StartsWith("XE ", StringComparison.OrdinalIgnoreCase)) return null;
        var rest = text[3..].Trim();
        if (rest.StartsWith('"'))
        {
            var end = rest.IndexOf('"', 1);
            while (end > 0 && rest[end - 1] == '\\') end = rest.IndexOf('"', end + 1);
            rest = end > 0 ? rest[1..end] : rest[1..];
        }

        return rest.Replace("\\\"", "\"", StringComparison.Ordinal);
    }
}
