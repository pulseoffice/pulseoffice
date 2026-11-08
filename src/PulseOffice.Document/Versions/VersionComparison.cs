using PulseOffice.Document.Comparison;

namespace PulseOffice.Document.Versions;

public enum VersionChangeKind
{
    Added,
    Removed,
    Changed,
}

public enum VersionRunKind
{
    Same,
    Added,
    Removed,
}

public sealed record VersionRun(string Text, VersionRunKind Kind);

public sealed record VersionChange(VersionChangeKind Kind, string Location, IReadOnlyList<VersionRun> Runs);

public sealed record VersionComparison(IReadOnlyList<VersionChange> Changes)
{
    public int Added => Changes.Count(change => change.Kind == VersionChangeKind.Added);

    public int Removed => Changes.Count(change => change.Kind == VersionChangeKind.Removed);

    public int Changed => Changes.Count(change => change.Kind == VersionChangeKind.Changed);

    public bool IsEmpty => Changes.Count == 0;
}

public sealed record VersionItem(string Location, string Text);

public interface IVersionTextDiff
{
    IReadOnlyList<VersionRun> Runs(string before, string after);
}

public sealed class VersionTextDiff(ISequenceDiff diff, ITextTokens tokens) : IVersionTextDiff
{
    public IReadOnlyList<VersionRun> Runs(string before, string after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var old = tokens.Split(before);
        var current = tokens.Split(after);
        var runs = new List<VersionRun>();
        foreach (var step in diff.Compare(old, current, StringComparer.Ordinal))
        {
            var (text, kind) = step.Operation switch
            {
                DiffOperation.Equal => (current[step.NewIndex], VersionRunKind.Same),
                DiffOperation.Deleted => (old[step.OldIndex], VersionRunKind.Removed),
                _ => (current[step.NewIndex], VersionRunKind.Added),
            };
            if (runs.Count > 0 && runs[^1].Kind == kind) runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
            else runs.Add(new VersionRun(text, kind));
        }

        return RemovedFirst(runs);
    }

    private static List<VersionRun> RemovedFirst(List<VersionRun> runs)
    {
        for (var index = 0; index + 1 < runs.Count; index++)
        {
            if (runs[index].Kind != VersionRunKind.Added || runs[index + 1].Kind != VersionRunKind.Removed) continue;
            (runs[index], runs[index + 1]) = (runs[index + 1], runs[index]);
            index++;
        }

        for (var index = 0; index + 1 < runs.Count; index++)
        {
            if (runs[index].Kind != VersionRunKind.Removed || runs[index + 1].Kind != VersionRunKind.Added) continue;
            if (char.IsWhiteSpace(runs[index].Text[^1]) || char.IsWhiteSpace(runs[index + 1].Text[0])) continue;
            runs.Insert(index + 1, new VersionRun(" ", VersionRunKind.Same));
            index++;
        }

        return runs;
    }
}

public interface IVersionSequenceComparer
{
    VersionComparison Compare(IReadOnlyList<VersionItem> saved, IReadOnlyList<VersionItem> current);
}

public sealed class VersionSequenceComparer(ISequenceDiff diff, IVersionTextDiff words) : IVersionSequenceComparer
{
    private sealed record Gap(List<int> Removed, List<int> Added);

    public VersionComparison Compare(IReadOnlyList<VersionItem> saved, IReadOnlyList<VersionItem> current)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(current);

        var steps = diff.Compare(saved.Select(item => item.Text).ToList(), current.Select(item => item.Text).ToList(), StringComparer.Ordinal);
        var gaps = new List<Gap>();
        Gap? open = null;
        foreach (var step in steps)
        {
            if (step.Operation == DiffOperation.Equal)
            {
                open = null;
                continue;
            }

            if (open is null)
            {
                open = new Gap([], []);
                gaps.Add(open);
            }

            if (step.Operation == DiffOperation.Deleted) open.Removed.Add(step.OldIndex);
            else open.Added.Add(step.NewIndex);
        }

        var changes = new List<VersionChange>();
        foreach (var gap in gaps)
        {
            var pairs = Math.Min(gap.Removed.Count, gap.Added.Count);
            for (var index = 0; index < pairs; index++)
            {
                var before = saved[gap.Removed[index]];
                var after = current[gap.Added[index]];
                changes.Add(new VersionChange(VersionChangeKind.Changed, after.Location, words.Runs(before.Text, after.Text)));
            }

            foreach (var removed in gap.Removed.Skip(pairs))
                changes.Add(new VersionChange(VersionChangeKind.Removed, saved[removed].Location, [new VersionRun(saved[removed].Text, VersionRunKind.Removed)]));
            foreach (var added in gap.Added.Skip(pairs))
                changes.Add(new VersionChange(VersionChangeKind.Added, current[added].Location, [new VersionRun(current[added].Text, VersionRunKind.Added)]));
        }

        return new VersionComparison(changes);
    }
}
