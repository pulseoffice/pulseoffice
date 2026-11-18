namespace PulseOffice.Sheets;

public interface ICommentMentions
{
    IReadOnlyList<CommentMention> Kept(string text, IEnumerable<CommentMention> mentions);

    IReadOnlyList<CommentMention> Detect(string text, IEnumerable<string> people);
}

public sealed class CommentMentions : ICommentMentions
{
    public IReadOnlyList<CommentMention> Kept(string text, IEnumerable<CommentMention> mentions)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(mentions);
        return [.. mentions
            .Where(mention => mention.Start >= 0 && mention.Length > 0 && mention.Start + mention.Length <= text.Length && mention.Person.Length > 0)
            .Where(mention => Names(text.Substring(mention.Start, mention.Length), mention.Person))
            .OrderBy(mention => mention.Start)];
    }

    public IReadOnlyList<CommentMention> Detect(string text, IEnumerable<string> people)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(people);
        var names = people.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).OrderByDescending(name => name.Length).ToList();
        var found = new List<CommentMention>();
        for (var index = text.IndexOf('@', StringComparison.Ordinal); index >= 0 && names.Count > 0; index = text.IndexOf('@', index + 1))
        {
            if (index > 0 && char.IsLetterOrDigit(text[index - 1])) continue;
            var start = index + 1;
            var name = names.FirstOrDefault(candidate => start + candidate.Length <= text.Length
                && string.Compare(text, start, candidate, 0, candidate.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (start + candidate.Length == text.Length || !char.IsLetterOrDigit(text[start + candidate.Length])));
            if (name is null) continue;
            found.Add(new CommentMention(index, name.Length + 1, name));
        }

        return found;
    }

    private bool Names(string covered, string person) =>
        string.Equals(covered.TrimStart('@', '+'), person, StringComparison.OrdinalIgnoreCase);
}
