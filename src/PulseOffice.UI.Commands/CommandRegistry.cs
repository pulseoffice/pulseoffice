using System.Collections.Concurrent;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;

namespace PulseOffice.UI.Commands;

public sealed record CommandNode(string Segment, CommandDefinition? Command, IReadOnlyList<CommandNode> Children)
{
    public bool IsGroup => Command is null;
}

public sealed class CommandRegistry(ITextCatalog texts)
{
    private readonly Dictionary<string, ICommandLayout> _layouts = [];

    public void Use(CommandSurface surface, ICommandLayout layout)
    {
        _layouts[PrefixOf(surface)] = layout;
        Interlocked.Increment(ref _version);
    }

    private int _version;
    private readonly SurfaceTreeCache _trees = new();

    private ICommandLayout? LayoutFor(string prefix) =>
        _layouts.TryGetValue(prefix, out var layout) ? layout : null;

    private string PrefixOf(CommandSurface surface) => surface switch
    {
        CommandSurface.Ribbon => "ribbon",
        CommandSurface.ContextMenu => "context",
        _ => "menu",
    };

    private readonly IReadOnlyList<SurfacePlacement> _placements = [new MenuPlacement(), new RibbonPlacement(), new ContextPlacement()];
    private readonly CommandScorer _scorer = new();
    private readonly ShortcutAudit _shortcuts = new();
    private readonly GestureKey _gestures = new();
    private readonly StructureKeyCollector _structure = new();

    private readonly ConcurrentDictionary<string, CommandDefinition> _commands = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, string?> _factoryGestures = new(StringComparer.Ordinal);

    public event EventHandler? BindingsChanged;

    private readonly List<string> _order = [];

    private readonly Lock _gate = new();

    public string TitleOf(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.TitleOverride ?? texts[command.TitleKey];
    }

    public string? MenuTrailOf(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.MenuPath is not { Length: > 0 } path) return null;

        var key = "menu";
        var parts = new List<string>();

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            key = $"{key}.{segment}";
            parts.Add(texts[key]);
        }

        return string.Join(" › ", parts);
    }

    public CommandPresentation Present(CommandDefinition command) =>
    new(command, TitleOf(command), DescriptionOf(command), MenuTrailOf(command), command.DefaultGesture)
    {
        Category = CategoryOf(command),
    };

    public string? CategoryOf(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.MenuPath?.Split('/', StringSplitOptions.RemoveEmptyEntries) is [var root, ..]
            ? texts[$"menu.{root}"]
            : null;
    }

    public string? CategoryKeyOf(CommandDefinition command) =>
        command.MenuPath?.Split('/', StringSplitOptions.RemoveEmptyEntries) is [var root, ..] ? root : null;

    public IReadOnlyList<string> KeywordsOf(CommandDefinition command) =>
        texts.Contains(command.KeywordsKey)
            ? [.. command.Keywords, .. texts[command.KeywordsKey].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : command.Keywords;

    public string? DescriptionOf(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return texts.Contains(command.DescriptionKey) ? texts[command.DescriptionKey] : null;
    }

    public void Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        foreach (var placement in _placements)
            if (LayoutFor(placement.Prefix) is { } layout) command = placement.Place(command, layout);

        if (!_commands.TryAdd(command.Id.Value, command))
            throw new InvalidOperationException($"Duplicate command: '{command.Id}'.");

        _factoryGestures[command.Id.Value] = command.DefaultGesture;

        lock (_gate) _order.Add(command.Id.Value);
        Interlocked.Increment(ref _version);
    }

    public string? FactoryGestureOf(CommandId id) =>
        _factoryGestures.TryGetValue(id.Value, out var gesture) ? gesture : Find(id)?.DefaultGesture;

    public int MenuRootRank(string root) => LayoutFor("menu")?.RootRank(root) ?? 0;

    public void NotifyBindingsChanged() => BindingsChanged?.Invoke(this, EventArgs.Empty);

    public void RegisterRange(IEnumerable<CommandDefinition> commands)
    {
        foreach (var c in commands) Register(c);
    }

    public bool Unregister(CommandId id)
    {
        if (!_commands.TryRemove(id.Value, out _)) return false;

        _factoryGestures.TryRemove(id.Value, out _);

        lock (_gate) _order.Remove(id.Value);
        Interlocked.Increment(ref _version);
        return true;
    }

    public bool Rebind(CommandId id, string? gesture)
    {
        lock (_gate)
        {
            if (!_commands.TryGetValue(id.Value, out var command)) return false;

            _commands[id.Value] = command with
            {
                DefaultGesture = string.IsNullOrWhiteSpace(gesture) ? null : gesture.Trim(),
            };
            Interlocked.Increment(ref _version);

            return true;
        }
    }

    public CommandDefinition? Find(CommandId id) =>
        _commands.TryGetValue(id.Value, out var c) ? c : null;

    public IReadOnlyCollection<CommandDefinition> All => [.. Ordered()];

    private IEnumerable<CommandDefinition> Ordered()
    {
        string[] ids;
        lock (_gate) ids = [.. _order];

        foreach (var id in ids)
            if (_commands.TryGetValue(id, out var command))
                yield return command;
    }

    public IEnumerable<CommandDefinition> Applicable(CommandContext ctx, CommandSurface surface = CommandSurface.None) =>
    OnSurface(surface).Where(command => command.IsEnabled(ctx));

    public IEnumerable<CommandDefinition> Present(CommandContext ctx, CommandSurface surface = CommandSurface.None) =>
    OnSurface(surface).Where(command => command.IsPresent(ctx));

    private IEnumerable<CommandDefinition> OnSurface(CommandSurface surface) =>
    Ordered().Where(command => surface == CommandSurface.None || (command.Surface & surface) != 0);

    public IReadOnlyList<CommandNode> BuildMenu(CommandContext ctx) =>
    CachedTree(Present(ctx, CommandSurface.Menu), c => c.MenuPath, "menu", ctx.Module);

    public IReadOnlyList<CommandNode> BuildContextMenu(CommandContext ctx, string root) =>
        BuildTree(
            Applicable(ctx, CommandSurface.ContextMenu)
                .Where(command => PathIn(command, root) is not null),
            command => PathIn(command, root), "context", ctx.Module);

    private string? PathIn(CommandDefinition command, string root) =>
        command.ContextPaths.FirstOrDefault(path =>
            path.Equals(root, StringComparison.Ordinal)
            || path.StartsWith(root + "/", StringComparison.Ordinal));

    public IReadOnlyList<CommandNode> BuildRibbon(CommandContext ctx) =>
    CachedTree(Present(ctx, CommandSurface.Ribbon), c => c.RibbonPathIn(ctx.Module), "ribbon", ctx.Module);

    private IReadOnlyList<CommandNode> CachedTree(
        IEnumerable<CommandDefinition> commands, Func<CommandDefinition, string?> pathSelector, string prefix, ModuleKind module)
    {
        CommandDefinition[] present = [.. commands];
        var stamp = new SurfaceTreeStamp(prefix, module, texts.Language, Volatile.Read(ref _version));
        return _trees.GetOrBuild(stamp, present, () => BuildTree(present, pathSelector, prefix, module));
    }

    public IEnumerable<CommandDefinition> Search(string query, CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Applicable(ctx, CommandSurface.Palette);

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Applicable(ctx, CommandSurface.Palette)
            .Select(c => (Command: c, Score: Score(c, query, terms)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => TitleOf(x.Command), StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Command);
    }

    private int Score(CommandDefinition command, string query, string[] terms) =>
        _scorer.Score(query, terms, TitleOf(command), KeywordsOf(command), DescriptionOf(command), MenuTrailOf(command));

    public IReadOnlyDictionary<string, CommandDefinition> ActiveGestures(CommandContext ctx)
    {
        var chosen = new Dictionary<string, (string Gesture, CommandDefinition Command)>(StringComparer.Ordinal);
        foreach (var c in Applicable(ctx).Where(c => !string.IsNullOrWhiteSpace(c.DefaultGesture)))
        {
            var key = _gestures.Canonical(c.DefaultGesture!);
            if (key.Length == 0) continue;

            if (chosen.TryGetValue(key, out var existing) &&
                existing.Command.Module != ModuleKind.Shell && c.Module == ModuleKind.Shell)
                continue;
            chosen[key] = (c.DefaultGesture!, c);
        }

        var map = new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (gesture, command) in chosen.Values) map[gesture] = command;
        return map;
    }

    public IReadOnlyList<string> UnplannedCommands(CommandSurface surface)
    {
        var prefix = PrefixOf(surface);

        return LayoutFor(prefix) is not { } layout
            ? []
            : [.. All
                .Where(command => command.Surface.HasFlag(surface))
                .Where(command => layout.PathFor(command.Id.Value) is null)
                .Select(command => command.Id.Value)
                .Order()];
    }

    public IReadOnlyList<string> ConflictingShortcuts() => _shortcuts.Conflicts(All);

    public IReadOnlyList<string> DeadLayoutRules(CommandSurface surface) =>
        LayoutFor(PrefixOf(surface)) is not { } layout
            ? []
            : layout.UnusedRules(All.Select(command => command.Id.Value));

    public IReadOnlyList<string> StructureKeys() => _structure.Collect(All);

    private IReadOnlyList<CommandNode> BuildTree(
        IEnumerable<CommandDefinition> commands, Func<CommandDefinition, string?> pathSelector, string prefix, ModuleKind module) =>
        new CommandTreeBuilder(
                prefix, key => texts[key], TitleOf,
                LayoutFor(prefix) is { } layout ? new LayoutOrdering(layout, module) : new FixedOrdering(prefix))
            .Build(commands, pathSelector);
}

internal abstract class SurfacePlacement
{
    public abstract string Prefix { get; }

    public abstract CommandDefinition Place(CommandDefinition command, ICommandLayout layout);
}

internal sealed class MenuPlacement : SurfacePlacement
{
    public override string Prefix => "menu";

    public override CommandDefinition Place(CommandDefinition command, ICommandLayout layout) =>
        layout.PathFor(command.Id.Value) is { } path && !string.IsNullOrWhiteSpace(path)
            ? command with { MenuPath = path, Surface = command.Surface | CommandSurface.Menu }
            : command;
}

internal sealed class RibbonPlacement : SurfacePlacement
{
    public override string Prefix => "ribbon";

    public override CommandDefinition Place(CommandDefinition command, ICommandLayout layout)
    {
        var path = layout.PathFor(command.Id.Value);
        var modulePaths = layout.ModulePathsFor(command.Id.Value);

        return string.IsNullOrWhiteSpace(path) && modulePaths.Count == 0
            ? command with { RibbonPath = null, RibbonPaths = modulePaths, Surface = command.Surface & ~CommandSurface.Ribbon }
            : command with
            {
                RibbonPath = string.IsNullOrWhiteSpace(path) ? null : path,
                RibbonPaths = modulePaths,
                Surface = command.Surface | CommandSurface.Ribbon,
            };
    }
}

internal sealed class CommandScorer
{
    private readonly SearchFolding _folding = new();

    private readonly IReadOnlyList<(int Score, Func<CommandText, string, bool> Matches)> _rules =
    [
        (100, (text, term) => text.Title.StartsWith(term, StringComparison.Ordinal)),
        (60, (text, term) => text.Title.Contains(term, StringComparison.Ordinal)),
        (40, (text, term) => text.Keywords.Any(keyword => keyword.Contains(term, StringComparison.Ordinal))),
        (20, (text, term) => text.Description?.Contains(term, StringComparison.Ordinal) == true),
        (10, (text, term) => text.Trail?.Contains(term, StringComparison.Ordinal) == true),
    ];

    private const int PhraseScore = 90;

    private const int ExactTitleScore = 200;

    public int Score(string query, string[] terms, string title, IReadOnlyList<string> keywords, string? description, string? trail)
    {
        var text = new CommandText(
            _folding.Fold(title), [.. keywords.Select(_folding.Fold)],
            description is null ? null : _folding.Fold(description), trail is null ? null : _folding.Fold(trail));

        var phrase = _folding.Fold(query.Trim());
        var phraseBonus = terms.Length > 1 && (text.Title.Contains(phrase, StringComparison.Ordinal)
            || text.Keywords.Any(keyword => keyword.Contains(phrase, StringComparison.Ordinal)))
            ? PhraseScore * terms.Length
            : 0;

        var total = 0;

        foreach (var term in terms.Select(_folding.Fold))
        {
            var best = _rules.FirstOrDefault(rule => rule.Matches(text, term)).Score;
            if (best == 0) return phraseBonus;
            total += best;
        }

        var exact = string.Equals(Bare(text.Title), Bare(phrase), StringComparison.Ordinal) ? ExactTitleScore : 0;
        return Math.Max(total, phraseBonus) + exact;
    }

    private string Bare(string text) => text.TrimEnd('…', '.', ' ');

    private sealed record CommandText(string Title, IReadOnlyList<string> Keywords, string? Description, string? Trail);
}

public sealed class SearchFolding
{
    public string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character == '…' ? ' ' : char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC).Trim();
    }
}

public sealed class GestureKey
{
    private readonly Shortcuts.GestureChordParser _parser = new();

    public string Canonical(string gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);

        if (_parser.Parse(gesture) is { } chord) return chord.Canonical().ToLowerInvariant();

        var text = gesture.Trim();
        if (text.Length == 0) return string.Empty;

        var plusKey = text.EndsWith("++", StringComparison.Ordinal) || text == "+";
        if (plusKey) text = text[..^1];

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToLowerInvariant())
            .ToList();

        if (plusKey) parts.Add("+");

        return parts.Count == 0 ? string.Empty : string.Join('+', [.. parts.Take(parts.Count - 1).Order(StringComparer.Ordinal), parts[^1]]);
    }
}

internal sealed class ShortcutAudit
{
    private readonly GestureKey _key = new();
    private readonly Shortcuts.ShortcutScope _scope = new();

    public IReadOnlyList<string> Conflicts(IEnumerable<CommandDefinition> commands) =>
        [.. commands
            .Where(command => command.DefaultGesture is { Length: > 0 })
            .GroupBy(command => Canonical(command.DefaultGesture!), StringComparer.Ordinal)
            .Where(group => group.Count() > 1 && ActiveTogether(group.ToList()))
            .Select(group => $"{group.First().DefaultGesture}: {string.Join(", ", group.Select(command => command.Id.Value).Order(StringComparer.Ordinal))}")
            .Order(StringComparer.Ordinal)];

    private bool ActiveTogether(IReadOnlyList<CommandDefinition> commands)
    {
        for (var i = 0; i < commands.Count; i++)
            for (var j = i + 1; j < commands.Count; j++)
                if (_scope.ActiveTogether(commands[i], commands[j]))
                    return true;

        return false;
    }

    private string Canonical(string gesture) => _key.Canonical(gesture);
}

internal sealed class StructureKeyCollector
{
    public IReadOnlyList<string> Collect(IEnumerable<CommandDefinition> commands)
    {
        var keys = new List<string>();

        foreach (var command in commands)
        {
            Collect(keys, "menu", command.MenuPath);
            Collect(keys, "ribbon", command.RibbonPath);
            foreach (var path in command.RibbonPaths.Values) Collect(keys, "ribbon", path);
            foreach (var path in command.ContextPaths) Collect(keys, "context", path);
        }

        return [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private void Collect(List<string> keys, string prefix, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var key = prefix;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            key = $"{key}.{segment}";
            keys.Add(key);
        }
    }
}

internal sealed record SurfaceTreeStamp(string Prefix, ModuleKind Module, UiLanguage Language, int Version);

internal sealed class SurfaceTreeCache
{
    private readonly Dictionary<(string Prefix, ModuleKind Module), Entry> _entries = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<CommandNode> GetOrBuild(SurfaceTreeStamp stamp, CommandDefinition[] present, Func<IReadOnlyList<CommandNode>> build)
    {
        var key = (stamp.Prefix, stamp.Module);
        lock (_gate)
            if (_entries.TryGetValue(key, out var entry) && entry.Matches(stamp, present))
                return entry.Tree;

        var tree = build();
        lock (_gate) _entries[key] = new Entry(stamp, present, tree);
        return tree;
    }

    private sealed record Entry(SurfaceTreeStamp Stamp, CommandDefinition[] Present, IReadOnlyList<CommandNode> Tree)
    {
        public bool Matches(SurfaceTreeStamp stamp, CommandDefinition[] present)
        {
            if (Stamp != stamp || Present.Length != present.Length) return false;
            for (var index = 0; index < present.Length; index++)
                if (!ReferenceEquals(Present[index], present[index])) return false;
            return true;
        }
    }
}

internal abstract class NodeOrdering
{
    public abstract IEnumerable<TreeNode> Roots(List<TreeNode> roots);

    public virtual void Sort(TreeNode node)
    {
    }
}

internal sealed class LayoutOrdering(ICommandLayout layout, ModuleKind module) : NodeOrdering
{
    public override IEnumerable<TreeNode> Roots(List<TreeNode> roots) =>
        roots.Select((node, position) => (node, position))
            .OrderBy(entry => layout.RootRank(entry.node.Key[(entry.node.Key.IndexOf('.') + 1)..]))
            .ThenBy(entry => Rank(entry.node))
            .ThenBy(entry => entry.position)
            .Select(entry => entry.node);

    public override void Sort(TreeNode node)
    {
        foreach (var child in node.Children) Sort(child);
        if (node.Children.Count < 2) return;

        var ranked = ByRank(node.Children).ToList();
        node.Children.Clear();
        node.Children.AddRange(ranked);
    }

    private IEnumerable<TreeNode> ByRank(List<TreeNode> nodes) =>
        nodes.Select((node, position) => (node, position))
            .OrderBy(entry => Rank(entry.node))
            .ThenBy(entry => entry.position)
            .Select(entry => entry.node);

    private int Rank(TreeNode node) =>
        node.Command is { } command
            ? layout.RankOf(command.Id.Value, module)
            : node.Children.Count == 0 ? int.MaxValue : node.Children.Min(Rank);
}

internal sealed class FixedOrdering(string prefix) : NodeOrdering
{
    private readonly string[] _order = prefix == "menu"
        ? ["file", "edit", "view", "insert", "format", "sheet", "data", "tools", "window", "help"]
        : ["home", "insert", "format", "sheet", "data", "review", "view"];

    public override IEnumerable<TreeNode> Roots(List<TreeNode> roots) =>
        roots.Select((node, position) => (node, position))
            .OrderBy(entry => Rank(entry.node.Key))
            .ThenBy(entry => entry.position)
            .Select(entry => entry.node);

    private int Rank(string key)
    {
        var segment = key.StartsWith($"{prefix}.", StringComparison.Ordinal) ? key[(prefix.Length + 1)..] : key;
        var index = Array.IndexOf(_order, segment);
        return index < 0 ? _order.Length : index;
    }
}

internal sealed class TreeNode(string segment, string key)
{
    public string Segment { get; } = segment;
    public string Key { get; } = key;
    public CommandDefinition? Command { get; init; }
    public List<TreeNode> Children { get; } = [];

    public CommandNode Freeze() => new(Segment, Command, [.. Children.Select(child => child.Freeze())]);
}

internal sealed class CommandTreeBuilder(
    string prefix, Func<string, string> translate, Func<CommandDefinition, string> titleOf, NodeOrdering ordering)
{
    public IReadOnlyList<CommandNode> Build(IEnumerable<CommandDefinition> commands, Func<CommandDefinition, string?> pathSelector)
    {
        var roots = new List<TreeNode>();

        foreach (var command in commands)
        {
            var path = pathSelector(command);
            if (string.IsNullOrWhiteSpace(path)) continue;

            var level = roots;
            TreeNode? current = null;
            var key = prefix;

            foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                key = $"{key}.{segment}";
                var accumulated = key;
                current = level.FirstOrDefault(node => node.Key == accumulated);

                if (current is null)
                {
                    current = new TreeNode(translate(key), key);
                    level.Add(current);
                }

                level = current.Children;
            }

            current?.Children.Add(new TreeNode(titleOf(command), command.Id.Value) { Command = command });
        }

        foreach (var root in roots) ordering.Sort(root);
        return [.. ordering.Roots(roots).Select(node => node.Freeze())];
    }
}
