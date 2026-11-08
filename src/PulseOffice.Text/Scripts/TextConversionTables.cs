using System.Text;

namespace PulseOffice.Text.Scripts;

public enum TextConversionKind { SimplifiedToTraditional, TraditionalToSimplified, HangulToHanja, HanjaToHangul }

public sealed record TextConversionTable(
    TextConversionKind Kind,
    IReadOnlyDictionary<string, string> Words,
    IReadOnlyDictionary<string, string> Characters)
{
    public bool IsEmpty => Words.Count == 0 && Characters.Count == 0;
}

public interface ITextConversionTables
{
    IReadOnlyList<TextConversionKind> Available { get; }

    TextConversionTable? For(TextConversionKind kind);
}

public interface ITextConverter
{
    string Convert(string text, TextConversionTable table);
}

public sealed class TextConverter : ITextConverter
{
    public string Convert(string text, TextConversionTable table)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(table);

        var longest = table.Words.Count == 0 ? 0 : table.Words.Keys.Max(word => word.Length);
        var result = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var matched = false;

            for (var length = Math.Min(longest, text.Length - index); length > 1; length--)
            {
                if (!table.Words.TryGetValue(text.Substring(index, length), out var replacement)) continue;

                result.Append(replacement);
                index += length;
                matched = true;
                break;
            }

            if (matched) continue;

            var unit = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                ? text.Substring(index, 2)
                : text.Substring(index, 1);

            result.Append(table.Characters.TryGetValue(unit, out var character) ? character : unit);

            index += unit.Length;
        }

        return result.ToString();
    }
}

public sealed class TextConversionTableReader
{
    public TextConversionTable Read(TextConversionKind kind, string? characterFile, string? wordFile)
    {
        var reversed = Reversed(kind);
        var characters = new Dictionary<string, string>(StringComparer.Ordinal);
        var words = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in Lines(characterFile))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;

            var sources = Elements(parts[0].Trim());
            var targets = Elements(parts[1].Trim());
            if (sources.Count != 1 || targets.Count == 0) continue;

            if (reversed)
            {
                foreach (var target in targets) characters.TryAdd(target, sources[0]);
            }
            else
            {
                characters.TryAdd(sources[0], targets[0]);
            }
        }

        foreach (var line in Lines(wordFile))
        {
            var parts = line.Split('>', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) continue;

            words[parts[0]] = parts[1];
        }

        return reversed
            ? new TextConversionTable(kind,
                words.GroupBy(pair => pair.Value).ToDictionary(group => group.Key, group => group.First().Key),
                characters)
            : new TextConversionTable(kind, words, characters);
    }

    private static List<string> Elements(string text)
    {
        var elements = new List<string>();
        foreach (var rune in text.EnumerateRunes())
            if (!Rune.IsWhiteSpace(rune) && rune.Value is not (',' or ';' or '|'))
                elements.Add(rune.ToString());
        return elements;
    }

    private static bool Reversed(TextConversionKind kind) =>
        kind is TextConversionKind.TraditionalToSimplified or TextConversionKind.HanjaToHangul;

    private static IEnumerable<string> Lines(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return [];

        try
        {
            return File.ReadAllLines(path, Encoding.UTF8)
                .Select(line => line.TrimStart('\uFEFF'))
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

public sealed class FolderTextConversionTables(string folder, TextConversionTableReader reader)
    : ITextConversionTables
{
    private readonly Dictionary<TextConversionKind, TextConversionTable> _tables = [];

    public string Folder { get; } = folder;

    public IReadOnlyList<TextConversionKind> Available =>
        [.. Enum.GetValues<TextConversionKind>().Where(kind => For(kind) is not null)];

    public TextConversionTable? For(TextConversionKind kind)
    {
        if (_tables.TryGetValue(kind, out var known)) return known.IsEmpty ? null : known;

        var table = kind is TextConversionKind.SimplifiedToTraditional or TextConversionKind.TraditionalToSimplified
            ? reader.Read(kind, Path.Combine(Folder, "stc_char.dic"), Path.Combine(Folder, "stc_word.dic"))
            : reader.Read(kind, Path.Combine(Folder, "hhc_char.dic"), null);

        _tables[kind] = table;

        return table.IsEmpty ? null : table;
    }
}
