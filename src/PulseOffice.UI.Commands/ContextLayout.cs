using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands;

public interface IContextLayout : ICommandLayout
{
    IReadOnlyList<string> PathsFor(string commandId);
}

public sealed class ContextLayout(IReadOnlyList<(string Match, string Path)> rules) : IContextLayout
{
    public IReadOnlyList<string> PathsFor(string commandId)
    {
        ArgumentNullException.ThrowIfNull(commandId);

        var roots = new HashSet<string>(StringComparer.Ordinal);
        var paths = new List<string>();

        foreach (var (match, path) in rules)
            if (Matches(match, commandId) && roots.Add(RootOf(path)))
                paths.Add(path);

        return paths;
    }

    public string? PathFor(string commandId) => PathsFor(commandId).FirstOrDefault();

    public IReadOnlyDictionary<ModuleKind, string> ModulePathsFor(string commandId) =>
        new Dictionary<ModuleKind, string>();

    public int RankOf(string commandId, ModuleKind module)
    {
        for (var index = 0; index < rules.Count; index++)
            if (Matches(rules[index].Match, commandId)) return index;

        return int.MaxValue;
    }

    public int RootRank(string root) => 0;

    public IReadOnlyList<string> UnusedRules(IEnumerable<string> commandIds)
    {
        var ids = commandIds.ToList();

        return [.. rules
            .Where(rule => !rule.Match.EndsWith('*') && !rule.Match.EndsWith('.'))
            .Where(rule => !ids.Any(id => Matches(rule.Match, id)))
            .Select(rule => rule.Match)
            .Distinct(StringComparer.Ordinal)];
    }

    private bool Matches(string match, string commandId) =>
        match.EndsWith('*') ? commandId.StartsWith(match[..^1], StringComparison.Ordinal)
        : match.EndsWith('.') ? commandId.StartsWith(match, StringComparison.Ordinal)
        : string.Equals(match, commandId, StringComparison.Ordinal);

    private string RootOf(string path)
    {
        var separator = path.IndexOf('/', StringComparison.Ordinal);
        return separator < 0 ? path : path[..separator];
    }
}

internal sealed class ContextPlacement : SurfacePlacement
{
    public override string Prefix => "context";

    public override CommandDefinition Place(CommandDefinition command, ICommandLayout layout)
    {
        if (layout is not IContextLayout context) return command;

        var added = context.PathsFor(command.Id.Value);
        if (added.Count == 0) return command;

        var roots = added.Select(path => path.Split('/')[0]).ToHashSet(StringComparer.Ordinal);

        return command with
        {
            ContextPaths = [.. command.ContextPaths.Where(path => !roots.Contains(path.Split('/')[0])), .. added],
            Surface = command.Surface | CommandSurface.ContextMenu,
        };
    }
}
