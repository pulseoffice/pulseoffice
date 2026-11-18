namespace PulseOffice.UI.Controls.Review;

public sealed record MentionToken(int Start, string Prefix);

public sealed record MentionInsertion(string Text, int Caret);

public interface IMentionQuery
{
    MentionToken? TokenAt(string text, int caret);

    IReadOnlyList<string> Candidates(MentionToken token, IEnumerable<string> people);

    MentionInsertion Insert(string text, MentionToken token, int caret, string person);
}

public sealed class MentionQuery : IMentionQuery
{
    private const int MaxCandidates = 8;

    public MentionToken? TokenAt(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        caret = Math.Clamp(caret, 0, text.Length);
        for (var index = caret - 1; index >= 0; index--)
        {
            var letter = text[index];
            if (letter == '@')
            {
                if (index > 0 && char.IsLetterOrDigit(text[index - 1])) return null;
                return new MentionToken(index, text[(index + 1)..caret]);
            }

            if (letter == '\n' || caret - index > 40) return null;
        }

        return null;
    }

    public IReadOnlyList<string> Candidates(MentionToken token, IEnumerable<string> people)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(people);
        return [.. people
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Where(name => Matches(name, token.Prefix))
            .OrderBy(name => name.StartsWith(token.Prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxCandidates)];
    }

    public MentionInsertion Insert(string text, MentionToken token, int caret, string person)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(person);
        caret = Math.Clamp(caret, token.Start, text.Length);
        var mention = "@" + person + " ";
        return new MentionInsertion(text[..token.Start] + mention + text[caret..], token.Start + mention.Length);
    }

    private bool Matches(string name, string prefix) =>
        prefix.Length == 0
        || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        || name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
