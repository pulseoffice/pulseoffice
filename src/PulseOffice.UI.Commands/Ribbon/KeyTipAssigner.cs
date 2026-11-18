using System.Globalization;
using System.Text;

namespace PulseOffice.UI.Commands;

public interface IKeyTipAssigner
{
    IReadOnlyList<string> Assign(IReadOnlyList<string> titles);

    char? Typed(string? symbol);
}

public sealed class KeyTipAssigner : IKeyTipAssigner
{
    private const string Singles = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
    private const char OverflowPrefix = '0';

    public IReadOnlyList<string> Assign(IReadOnlyList<string> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);

        var used = new HashSet<char>();
        var tips = new string[titles.Count];

        for (var index = 0; index < titles.Count; index++)
            foreach (var candidate in Usable(Candidates(titles[index])))
                if (used.Add(candidate))
                {
                    tips[index] = candidate.ToString();
                    break;
                }

        var spare = new Queue<char>(Singles.Where(letter => !used.Contains(letter)));
        var overflow = new Queue<char>(Singles);

        for (var index = 0; index < titles.Count; index++)
        {
            if (tips[index] is not null) continue;
            if (spare.TryDequeue(out var letter)) tips[index] = letter.ToString();
            else if (overflow.TryDequeue(out var second)) tips[index] = string.Concat(OverflowPrefix, second);
            else tips[index] = string.Empty;
        }

        return tips;
    }

    public char? Typed(string? symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return null;
        var folded = Fold(symbol);
        return folded.Length == 1 && (Singles.Contains(folded[0]) || folded[0] == OverflowPrefix) ? folded[0] : null;
    }

    private static IEnumerable<char> Candidates(string title)
    {
        var folded = Fold(title);
        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words) yield return word[0];
        foreach (var word in words)
            foreach (var letter in word.Skip(1))
                yield return letter;
    }

    private static IEnumerable<char> Usable(IEnumerable<char> letters) => letters.Where(letter => letter != OverflowPrefix);

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var upper = char.ToUpperInvariant(character);
            builder.Append(upper is >= 'A' and <= 'Z' or >= '1' and <= '9' ? upper : upper == '0' ? '0' : ' ');
        }
        return builder.ToString().Trim();
    }
}
