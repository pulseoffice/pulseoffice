using System.Text;

namespace PulseOffice.Text.Spelling.Dictionaries;

public interface ITexHyphenationConverter
{
    string Convert(string patterns, string? exceptions);
}

public sealed class TexHyphenationConverter : ITexHyphenationConverter
{
    public string Convert(string patterns, string? exceptions)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        var output = new StringBuilder("UTF-8\n");

        foreach (var pattern in Tokens(patterns))
            if (!pattern.Contains('-', StringComparison.Ordinal) && !pattern.Contains('/', StringComparison.Ordinal))
                output.Append(pattern).Append('\n');

        if (exceptions is not null)
            foreach (var word in Tokens(exceptions))
                if (word.Contains('-', StringComparison.Ordinal))
                    output.Append(word.Replace('-', '=')).Append('\n');

        return output.ToString();
    }

    private IEnumerable<string> Tokens(string text) =>
        text.Split('\n')
            .Select(line => line.Split('%')[0])
            .SelectMany(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public interface ITexLicenseReader
{
    string? Read(string header);
}

public sealed class TexLicenseReader : ITexLicenseReader
{
    private static readonly string[] Preferred = ["MIT", "BSD", "Apache", "LPPL"];

    private const string AllPermissiveNotice =
        "copying and distribution of this file, with or without modification, are permitted in any medium without royalty provided the copyright notice and this notice are preserved";

    public string? Read(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        var licences = new List<string>();
        var notice = new System.Text.StringBuilder();
        var inside = false;
        string? name = null;

        foreach (var raw in header.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!line.StartsWith('%')) break;

            var body = line[1..];
            var trimmed = body.Trim();
            var indent = body.Length - body.TrimStart().Length;

            if (indent <= 1 && trimmed.EndsWith(':') && !trimmed.StartsWith('-'))
            {
                Flush();
                inside = trimmed.Equals("licence:", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("license:", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (indent <= 1 && trimmed.Contains(':', StringComparison.Ordinal) && !trimmed.StartsWith('-'))
            {
                Flush();
                inside = false;
                continue;
            }

            if (!inside) continue;

            notice.Append(' ').Append(trimmed);
            var item = trimmed.TrimStart('-').Trim();
            if (item.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                name = item[5..].Trim();
            }
            else if (item.StartsWith("version:", StringComparison.OrdinalIgnoreCase) && name is not null)
            {
                name = $"{name}-{item[8..].Trim()}";
            }
        }

        Flush();

        if (licences.Count == 0)
            return string.Join(' ', notice.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains(AllPermissiveNotice, StringComparison.OrdinalIgnoreCase) ? "FSFAP" : null;
        var ordered = licences
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(licence => Rank(licence))
            .ToList();
        return ordered.Count == 1 ? ordered[0] : string.Join(" OR ", ordered);

        void Flush()
        {
            if (name is { Length: > 0 }) licences.Add(name.Replace(' ', '-'));
            name = null;
        }
    }

    private int Rank(string licence)
    {
        var index = Array.FindIndex(Preferred, prefix => licence.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? Preferred.Length : index;
    }
}
