namespace PulseOffice.Sheets;

public interface IAnnotatedSpanMapping
{
    CommentTextSpan? Shown(string value, string shown, CommentTextSpan span);
}

public sealed class AnnotatedSpanMapping : IAnnotatedSpanMapping
{
    public CommentTextSpan? Shown(string value, string shown, CommentTextSpan span)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(span);
        if (span.Length <= 0 || span.Start < 0 || span.Start + span.Length > value.Length) return null;
        if (string.Equals(value, shown, StringComparison.Ordinal)) return span;

        var embedded = value.Length == 0 ? -1 : shown.IndexOf(value, StringComparison.Ordinal);
        if (embedded >= 0) return span with { Start = span.Start + embedded };

        var positions = Aligned(value, shown);
        if (positions[span.Start] is not { } first || positions[span.Start + span.Length - 1] is not { } last) return null;
        return new CommentTextSpan(first, last - first + 1);
    }

    private int?[] Aligned(string value, string shown)
    {
        var positions = new int?[value.Length];
        var cursor = 0;
        for (var index = 0; index < value.Length && cursor < shown.Length; index++)
        {
            var found = shown.IndexOf(value[index], cursor);
            if (found < 0) continue;
            positions[index] = found;
            cursor = found + 1;
        }
        return positions;
    }
}
