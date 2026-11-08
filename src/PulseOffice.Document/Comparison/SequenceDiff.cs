using System.Text.RegularExpressions;

namespace PulseOffice.Document.Comparison;

public enum DiffOperation
{
    Equal,
    Inserted,
    Deleted,
}

public sealed record DiffStep(DiffOperation Operation, int OldIndex, int NewIndex);

public interface ISequenceDiff
{
    IReadOnlyList<DiffStep> Compare<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, IEqualityComparer<T> equality);
}

public sealed class SequenceDiff : ISequenceDiff
{
    private const long CellLimit = 16_000_000;

    public IReadOnlyList<DiffStep> Compare<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, IEqualityComparer<T> equality)
    {
        var steps = new List<DiffStep>();
        var head = 0;
        while (head < before.Count && head < after.Count && equality.Equals(before[head], after[head])) steps.Add(new DiffStep(DiffOperation.Equal, head, head++));
        var tail = 0;
        while (tail < before.Count - head && tail < after.Count - head && equality.Equals(before[before.Count - 1 - tail], after[after.Count - 1 - tail])) tail++;
        var oldCount = before.Count - head - tail;
        var newCount = after.Count - head - tail;
        if ((long)oldCount * newCount > CellLimit)
        {
            for (var i = 0; i < oldCount; i++) steps.Add(new DiffStep(DiffOperation.Deleted, head + i, head));
            for (var j = 0; j < newCount; j++) steps.Add(new DiffStep(DiffOperation.Inserted, head + oldCount, head + j));
        }
        else
        {
            Middle(before, after, equality, head, oldCount, newCount, steps);
        }
        for (var k = 0; k < tail; k++) steps.Add(new DiffStep(DiffOperation.Equal, before.Count - tail + k, after.Count - tail + k));
        return steps;
    }

    private void Middle<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, IEqualityComparer<T> equality, int head, int oldCount, int newCount, List<DiffStep> steps)
    {
        var lengths = new int[oldCount + 1, newCount + 1];
        for (var i = oldCount - 1; i >= 0; i--)
        {
            for (var j = newCount - 1; j >= 0; j--)
            {
                lengths[i, j] = equality.Equals(before[head + i], after[head + j]) ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }
        var x = 0;
        var y = 0;
        while (x < oldCount || y < newCount)
        {
            if (x < oldCount && y < newCount && equality.Equals(before[head + x], after[head + y]))
            {
                steps.Add(new DiffStep(DiffOperation.Equal, head + x++, head + y++));
            }
            else if (y < newCount && (x >= oldCount || lengths[x, y + 1] >= lengths[x + 1, y]))
            {
                steps.Add(new DiffStep(DiffOperation.Inserted, head + x, head + y++));
            }
            else
            {
                steps.Add(new DiffStep(DiffOperation.Deleted, head + x++, head + y));
            }
        }
    }
}

public interface ITextTokens
{
    IReadOnlyList<string> Split(string text);
}

public sealed partial class WordTokens : ITextTokens
{
    public IReadOnlyList<string> Split(string text) => [.. Token().Matches(text).Select(match => match.Value)];

    [GeneratedRegex(@"\w+|\s+|[^\w\s]", RegexOptions.CultureInvariant)]
    private partial Regex Token();
}
