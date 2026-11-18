namespace PulseOffice.Sheets;

public interface IAnnotatedSpanFollowing
{
    CommentTextSpan? Follow(string before, string after, CommentTextSpan span);
}

public sealed class AnnotatedSpanFollowing : IAnnotatedSpanFollowing
{
    public CommentTextSpan? Follow(string before, string after, CommentTextSpan span)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(span);
        if (string.Equals(before, after, StringComparison.Ordinal)) return span;

        var shortest = Math.Min(before.Length, after.Length);
        var prefix = 0;
        while (prefix < shortest && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < shortest - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;

        var changedEnd = before.Length - suffix;
        var shift = after.Length - before.Length;
        var start = span.Start;
        var end = span.Start + span.Length;

        if (end <= prefix) return Within(start, end, after.Length);
        if (start >= changedEnd) return Within(start + shift, end + shift, after.Length);

        var newStart = start < prefix ? start : prefix;
        var newEnd = end > changedEnd ? end + shift : after.Length - suffix;
        return Within(newStart, newEnd, after.Length);
    }

    private CommentTextSpan? Within(int start, int end, int length)
    {
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);
        return end > start ? new CommentTextSpan(start, end - start) : null;
    }
}
