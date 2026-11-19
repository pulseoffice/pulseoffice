namespace PulseOffice.Sheets;

public interface IAnnotatedSpanMapping
{
    CommentTextSpan? Shown(string value, string shown, CommentTextSpan span);
}

public sealed class AnnotatedSpanMapping : IAnnotatedSpanMapping
{
    private const long AlignmentCellLimit = 1_000_000;

    public CommentTextSpan? Shown(string value, string shown, CommentTextSpan span)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(span);
        if (span.Length <= 0 || span.Start < 0 || span.Start + span.Length > value.Length) return null;
        if (string.Equals(value, shown, StringComparison.Ordinal)) return span;

        var embedded = value.Length == 0 ? -1 : shown.IndexOf(value, StringComparison.Ordinal);
        if (embedded >= 0) return span with { Start = span.Start + embedded };

        if ((long)value.Length * shown.Length > AlignmentCellLimit) return null;
        var positions = Aligned(value, shown);
        if (positions[span.Start] is not { } first || positions[span.Start + span.Length - 1] is not { } last) return null;
        return new CommentTextSpan(first, last - first + 1);
    }

    private int?[] Aligned(string value, string shown)
    {
        var lengths = new int[value.Length + 1, shown.Length + 1];
        for (var row = 1; row <= value.Length; row++)
            for (var column = 1; column <= shown.Length; column++)
                lengths[row, column] = value[row - 1] == shown[column - 1]
                    ? lengths[row - 1, column - 1] + 1
                    : Math.Max(lengths[row - 1, column], lengths[row, column - 1]);

        var positions = new int?[value.Length];
        var at = value.Length;
        var on = shown.Length;
        while (at > 0 && on > 0)
        {
            if (value[at - 1] == shown[on - 1] && lengths[at, on] == lengths[at - 1, on - 1] + 1)
            {
                positions[at - 1] = on - 1;
                at--;
                on--;
            }
            else if (lengths[at, on - 1] >= lengths[at - 1, on]) on--;
            else at--;
        }
        return positions;
    }
}
