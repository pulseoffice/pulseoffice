using System.Globalization;
using System.Text;
using PulseOffice.Documents.Indexes;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxIndexSettings
{
    private const string Marker = "urn:pulseoffice:index\n";

    private const int Limit = 4096;
    private const int MaximumLevel = 10;
    private const string HeadingKey = "heading-style";
    private const string EntryKey = "entry-style.";

    public string Encode(IndexDefinition index)
    {
        var lines = new StringBuilder(Marker);
        void Line(string key, string? value)
        {
            if (value is null) return;
            lines.Append(key).Append('=').Append(value.Replace("\n", " ", StringComparison.Ordinal)).Append('\n');
        }
        string Flag(bool value) => value ? "1" : "0";
        Line("kind", index.Kind.ToString());
        Line("levels", index.Levels.ToString(CultureInfo.InvariantCulture));
        Line("outline", Flag(index.FromOutline));
        Line("marks", Flag(index.FromMarks));
        Line("sequence", index.Sequence);
        Line("captions", index.Captions.ToString());
        Line("hyperlinks", Flag(index.Hyperlinks));
        Line("pages", Flag(index.PageNumbers));
        Line("combine", Flag(index.CombineEntries));
        Line("letters", Flag(index.LetterSeparators));
        Line("case", Flag(index.CaseSensitive));
        Line("user", index.UserName);
        Line("position", Flag(index.SortByPosition));
        Line("objects", ((int)index.ObjectSources).ToString(CultureInfo.InvariantCulture));
        Line(HeadingKey, index.LevelStyles.Heading);
        foreach (var (level, name) in index.LevelStyles.Entries) Line(EntryKey + level.ToString(CultureInfo.InvariantCulture), name);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(lines.ToString()));
    }

    public IndexDefinition Decode(string? data, IndexDefinition parsed)
    {
        if (Text(data) is not { } text) return parsed;
        var values = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .GroupBy(pair => pair[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.Ordinal);
        bool Flag(string key, bool fallback) => values.TryGetValue(key, out var value) ? value == "1" : fallback;
        string? Value(string key, string? fallback) => values.TryGetValue(key, out var value) && value.Length > 0 ? value : fallback;
        var kind = values.TryGetValue("kind", out var kindText) && Enum.TryParse<IndexKind>(kindText, out var readKind) && Enum.IsDefined(readKind) ? readKind : parsed.Kind;
        var levels = values.TryGetValue("levels", out var levelText) && int.TryParse(levelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var readLevels) ? Math.Clamp(readLevels, 1, 10) : parsed.Levels;
        var captions = values.TryGetValue("captions", out var captionText) && Enum.TryParse<CaptionDisplay>(captionText, out var readCaptions) && Enum.IsDefined(readCaptions) ? readCaptions : parsed.Captions;
        var objects = values.TryGetValue("objects", out var objectText) && int.TryParse(objectText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var readObjects)
            ? (IndexObjectSources)readObjects & IndexObjectSources.All
            : parsed.ObjectSources;
        return new IndexDefinition(kind)
        {
            Title = parsed.Title,
            Levels = levels,
            FromOutline = Flag("outline", parsed.FromOutline),
            FromMarks = Flag("marks", parsed.FromMarks),
            Sequence = Value("sequence", parsed.Sequence),
            Captions = captions,
            Hyperlinks = Flag("hyperlinks", parsed.Hyperlinks),
            PageNumbers = Flag("pages", parsed.PageNumbers),
            CombineEntries = Flag("combine", parsed.CombineEntries),
            LetterSeparators = Flag("letters", parsed.LetterSeparators),
            CaseSensitive = Flag("case", parsed.CaseSensitive),
            UserName = Value("user", parsed.UserName),
            SortByPosition = Flag("position", parsed.SortByPosition),
            ObjectSources = objects,
            LevelStyles = LevelStyles(values, parsed.LevelStyles),
        };
    }

    private IndexLevelStyles LevelStyles(IReadOnlyDictionary<string, string> values, IndexLevelStyles fallback)
    {
        var result = values.TryGetValue(HeadingKey, out var heading) && heading.Length > 0 ? fallback.WithHeading(heading) : fallback;
        foreach (var (key, name) in values)
        {
            if (!key.StartsWith(EntryKey, StringComparison.Ordinal) || name.Length == 0) continue;
            if (int.TryParse(key.AsSpan(EntryKey.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level is >= 0 and <= MaximumLevel)
                result = result.WithEntry(level, name);
        }
        return result;
    }

    private string? Text(string? data)
    {
        if (string.IsNullOrWhiteSpace(data) || data.Length > Limit) return null;
        var bytes = new byte[data.Length];
        if (!Convert.TryFromBase64String(data.Trim(), bytes, out var written)) return null;
        var text = Encoding.UTF8.GetString(bytes, 0, written);
        return text.StartsWith(Marker, StringComparison.Ordinal) ? text[Marker.Length..] : null;
    }
}
