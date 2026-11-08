namespace PulseOffice.Text.Spelling.Dictionaries;

public interface IDictionaryLicenseClassifier
{
    DictionaryLicense FromSpdx(string? expression, string? url = null);
}

public sealed class DictionaryLicenseClassifier : IDictionaryLicenseClassifier
{
    private static readonly string[] Permissive =
    [
        "MIT", "BSD", "APACHE", "ISC", "UNLICENSE", "CC0", "ZLIB", "X11", "LPPL", "PUBLIC", "PD", "WTFPL", "KNUTH", "0BSD", "CC-BY-", "FSFAP",
    ];

    private static readonly string[] Copyleft =
    [
        "GPL", "LGPL", "AGPL", "MPL", "EUPL", "CC-BY-SA", "CDDL", "EPL", "OSL",
    ];

    public DictionaryLicense FromSpdx(string? expression, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) return DictionaryLicense.Unknown with { Url = url };

        var text = expression.Trim();
        var tokens = Tokens(text);
        var position = 0;
        var freedom = Either(tokens, ref position, 0);
        return new DictionaryLicense(text, text, position == tokens.Count ? freedom : DictionaryFreedom.Unknown, url);
    }

    private const int MaximumNesting = 32;

    private List<string> Tokens(string expression) =>
        [.. expression.Replace("(", " ( ", StringComparison.Ordinal).Replace(")", " ) ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    private DictionaryFreedom Either(List<string> tokens, ref int position, int depth)
    {
        var freedom = Both(tokens, ref position, depth);

        while (position < tokens.Count && tokens[position].Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            position++;
            var alternative = Both(tokens, ref position, depth);
            freedom = alternative < freedom ? alternative : freedom;
        }

        return freedom;
    }

    private DictionaryFreedom Both(List<string> tokens, ref int position, int depth)
    {
        var freedom = Term(tokens, ref position, depth);

        while (position < tokens.Count && tokens[position].Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            position++;
            var required = Term(tokens, ref position, depth);
            freedom = required > freedom ? required : freedom;
        }

        return freedom;
    }

    private DictionaryFreedom Term(List<string> tokens, ref int position, int depth)
    {
        if (position >= tokens.Count || depth > MaximumNesting) return DictionaryFreedom.Unknown;

        if (tokens[position] == "(")
        {
            position++;
            var inner = Either(tokens, ref position, depth + 1);
            if (position >= tokens.Count || tokens[position] != ")") return DictionaryFreedom.Unknown;
            position++;
            return inner;
        }

        if (tokens[position] == ")") return DictionaryFreedom.Unknown;

        var identifier = tokens[position++];
        if (position + 1 < tokens.Count && tokens[position].Equals("WITH", StringComparison.OrdinalIgnoreCase)) position += 2;

        return Single(identifier);
    }

    private DictionaryFreedom Single(string identifier)
    {
        var upper = identifier.ToUpperInvariant();
        if (upper.StartsWith("CC-BY-", StringComparison.Ordinal)
            && (upper.Contains("-NC", StringComparison.Ordinal) || upper.Contains("-ND", StringComparison.Ordinal)))
            return DictionaryFreedom.Unknown;
        if (Copyleft.Any(prefix => upper.StartsWith(prefix, StringComparison.Ordinal))) return DictionaryFreedom.Copyleft;
        if (Permissive.Any(prefix => upper.StartsWith(prefix, StringComparison.Ordinal))) return DictionaryFreedom.Free;
        return DictionaryFreedom.Unknown;
    }
}

public interface IDictionaryRanking
{
    IReadOnlyList<DictionaryPackage> Order(IEnumerable<DictionaryPackage> packages, Func<string, int> priority);
}

public sealed class DictionaryRanking : IDictionaryRanking
{
    public IReadOnlyList<DictionaryPackage> Order(IEnumerable<DictionaryPackage> packages, Func<string, int> priority)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(priority);

        return [.. packages
            .OrderBy(package => Rank(package.License.Freedom))
            .ThenBy(package => priority(package.SourceId))
            .ThenBy(package => package.SourceId, StringComparer.Ordinal)];
    }

    private int Rank(DictionaryFreedom freedom) => freedom switch
    {
        DictionaryFreedom.Free => 0,
        DictionaryFreedom.Copyleft => 1,
        _ => 2,
    };
}
