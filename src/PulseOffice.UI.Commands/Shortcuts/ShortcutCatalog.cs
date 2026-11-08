using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;

namespace PulseOffice.UI.Commands.Shortcuts;

public sealed record ShortcutEntry(
    string Id,
    ModuleKind Module,
    string Title,
    string? Trail,
    string? MenuRoot,
    IReadOnlyList<string> Keywords,
    string? Gesture,
    string? FactoryGesture);

public interface IShortcutCatalog
{
    IReadOnlyList<ShortcutEntry> Entries();
}

public sealed class ShortcutCatalog(CommandRegistry registry) : IShortcutCatalog
{
    private static readonly ModuleKind[] ModuleOrder =
        [ModuleKind.Shell, ModuleKind.Sheets, ModuleKind.Presentations, ModuleKind.Documents];

    public IReadOnlyList<ShortcutEntry> Entries() =>
    [
        .. registry.All
            .Where(command => command.TitleOverride is null && command.Surface != CommandSurface.None)
            .Select(Entry)
            .OrderBy(entry => Array.IndexOf(ModuleOrder, entry.Module))
            .ThenBy(entry => entry.MenuRoot is null ? int.MaxValue : registry.MenuRootRank(entry.MenuRoot))
            .ThenBy(entry => entry.Trail ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase),
    ];

    private ShortcutEntry Entry(CommandDefinition command) => new(
        command.Id.Value,
        command.Module,
        registry.TitleOf(command).TrimEnd('…', '.').Trim(),
        registry.MenuTrailOf(command),
        command.MenuPath?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
        registry.KeywordsOf(command),
        command.DefaultGesture,
        registry.FactoryGestureOf(command.Id));
}

public abstract class ShortcutCategory(string key, string title)
{
    public string Key { get; } = key;

    public string Title { get; } = title;

    public abstract bool Contains(ShortcutEntry entry);
}

public sealed class AllShortcutsCategory(string title) : ShortcutCategory("all", title)
{
    public override bool Contains(ShortcutEntry entry) => true;
}

public sealed class ModuleShortcutCategory(ModuleKind module, string title)
    : ShortcutCategory($"module.{module.ToString().ToLowerInvariant()}", title)
{
    public ModuleKind Module { get; } = module;

    public override bool Contains(ShortcutEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Module == Module;
    }
}

public sealed class MenuShortcutCategory(string root, string title) : ShortcutCategory($"menu.{root}", title)
{
    public string Root { get; } = root;

    public override bool Contains(ShortcutEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.Equals(entry.MenuRoot, Root, StringComparison.Ordinal);
    }
}

public interface IShortcutCategories
{
    IReadOnlyList<ShortcutCategory> Build(IReadOnlyList<ShortcutEntry> entries);

    string ModuleTitle(ModuleKind module);
}

public sealed class ShortcutCategories(CommandRegistry registry, ITextCatalog texts) : IShortcutCategories
{
    private static readonly ModuleKind[] ModuleOrder =
        [ModuleKind.Shell, ModuleKind.Sheets, ModuleKind.Presentations, ModuleKind.Documents];

    public string ModuleTitle(ModuleKind module) =>
        texts[$"shortcuts.editor.module.{module.ToString().ToLowerInvariant()}"];

    public IReadOnlyList<ShortcutCategory> Build(IReadOnlyList<ShortcutEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var modules = ModuleOrder
            .Where(module => entries.Any(entry => entry.Module == module))
            .Select(module => (ShortcutCategory)new ModuleShortcutCategory(module, ModuleTitle(module)));

        var menus = entries
            .Select(entry => entry.MenuRoot)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(registry.MenuRootRank)
            .ThenBy(root => root, StringComparer.Ordinal)
            .Select(root => (ShortcutCategory)new MenuShortcutCategory(root, texts[$"menu.{root}"]));

        return [new AllShortcutsCategory(texts["shortcuts.editor.category.all"]), .. modules, .. menus];
    }
}
