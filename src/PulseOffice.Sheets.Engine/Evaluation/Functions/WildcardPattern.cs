using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class WildcardPattern(string wildcard, bool anchored, bool caseSensitive = false)
    : TextPattern(Translate(wildcard, anchored), caseSensitive, linearTime: true)
{
    public bool HasWildcards { get; } = DetectWildcards(wildcard);

    public override bool HasMetacharacters { get; } = DetectWildcards(wildcard) || wildcard.Contains('~', StringComparison.Ordinal);

    private static bool DetectWildcards(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '~') { i++; continue; }
            if (text[i] is '*' or '?') return true;
        }
        return false;
    }

    private static string Translate(string wildcard, bool anchored)
    {
        var builder = new StringBuilder(anchored ? "^" : string.Empty);

        for (var i = 0; i < wildcard.Length; i++)
        {
            var current = wildcard[i];
            if (current == '~' && i + 1 < wildcard.Length)
            {
                builder.Append(Regex.Escape(wildcard[++i].ToString()));
                continue;
            }
            builder.Append(current switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(current.ToString()),
            });
        }

        if (anchored) builder.Append('$');
        return builder.ToString();
    }
}
