using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands;

public interface ICommandLayout
{
    string? PathFor(string commandId);

    IReadOnlyDictionary<ModuleKind, string> ModulePathsFor(string commandId);

    int RankOf(string commandId, ModuleKind module);

    int RootRank(string root);

    IReadOnlyList<string> UnusedRules(IEnumerable<string> commandIds);
}

public sealed class CommandLayout(IReadOnlyList<(string Match, string Path)> rules, IReadOnlyList<string>? roots = null)
    : ICommandLayout
{
    public int RootRank(string root) =>
        roots is null ? 0 : roots.Select((name, index) => (Name: name, Index: index)).FirstOrDefault(entry => entry.Name == root, (Name: root, Index: roots.Count)).Index;

    private const char ModuleSeparator = '@';

    public string? PathFor(string commandId) =>
        Matched(commandId) is { } found ? rules[found].Path : null;

    public IReadOnlyDictionary<ModuleKind, string> ModulePathsFor(string commandId)
    {
        ArgumentNullException.ThrowIfNull(commandId);

        var paths = new Dictionary<ModuleKind, string>();

        foreach (var (match, path) in rules)
            if (Qualified(match) is { } qualified
                && string.Equals(qualified.Id, commandId, StringComparison.Ordinal))
                paths.TryAdd(qualified.Module, path);

        return paths;
    }

    public int RankOf(string commandId, ModuleKind module)
    {
        for (var index = 0; index < rules.Count; index++)
            if (Qualified(rules[index].Match) is { } qualified
                && qualified.Module == module
                && string.Equals(qualified.Id, commandId, StringComparison.Ordinal))
                return index;

        return Matched(commandId) ?? int.MaxValue;
    }

    public IReadOnlyList<string> Rules => [.. rules.Select(rule => rule.Match)];

    public IReadOnlyList<string> UnusedRules(IEnumerable<string> commandIds)
    {
        var ids = commandIds.ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<int>();

        foreach (var id in ids)
            if (Matched(id) is { } index) used.Add(index);

        for (var index = 0; index < rules.Count; index++)
            if (Qualified(rules[index].Match) is { } qualified && ids.Contains(qualified.Id))
                used.Add(index);

        return [.. rules
            .Select((rule, index) => (rule.Match, index))
            .Where(entry => !used.Contains(entry.index))
            .Where(entry => Stem(entry.Match) is not { } stem || Shadowed(stem, entry.index))
            .Select(entry => entry.Match)];
    }

    private bool Shadowed(string stem, int index)
    {
        for (var earlier = 0; earlier < index; earlier++)
            if (!rules[earlier].Match.Contains(ModuleSeparator, StringComparison.Ordinal)
                && Stem(rules[earlier].Match) is { } covering
                && stem.StartsWith(covering, StringComparison.Ordinal))
                return true;

        return false;
    }

    private static string? Stem(string match) =>
        match.EndsWith('*') ? match[..^1] : match.EndsWith('.') ? match : null;

    private static (string Id, ModuleKind Module)? Qualified(string match)
    {
        var separator = match.IndexOf(ModuleSeparator, StringComparison.Ordinal);
        if (separator < 0) return null;

        return Enum.TryParse<ModuleKind>(match[(separator + 1)..], out var module)
            ? (match[..separator], module)
            : null;
    }

    private int? Matched(string commandId)
    {
        ArgumentNullException.ThrowIfNull(commandId);

        for (var index = 0; index < rules.Count; index++)
        {
            var match = rules[index].Match;

            if (match.Contains(ModuleSeparator, StringComparison.Ordinal)) continue;

            if (match.EndsWith('*'))
            {
                if (commandId.StartsWith(match[..^1], StringComparison.Ordinal)) return index;
            }
            else if (match.EndsWith('.'))
            {
                if (commandId.StartsWith(match, StringComparison.Ordinal)) return index;
            }
            else if (string.Equals(commandId, match, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }
}
