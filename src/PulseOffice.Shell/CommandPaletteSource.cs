using PulseOffice.Localization;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public interface ICommandPaletteSource
{
    IReadOnlyList<CommandPaletteSection> Sections(string query, CommandContext context);

    void Chosen(CommandDefinition command);
}

public sealed class CommandPaletteSource(CommandRegistry registry, IRecentCommands recent, ITextCatalog texts) : ICommandPaletteSource
{
    private const int ResultLimit = 50;

    public IReadOnlyList<CommandPaletteSection> Sections(string query, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrWhiteSpace(query))
            return [new CommandPaletteSection(null, [.. registry.Search(query, context).Take(ResultLimit).Select(registry.Present)])];

        var applicable = registry.Applicable(context, CommandSurface.Palette).ToList();
        var available = new Dictionary<string, CommandDefinition>(StringComparer.Ordinal);
        foreach (var command in applicable) available.TryAdd(command.Id.Value, command);

        var recentItems = recent.Items
            .Select(id => available.GetValueOrDefault(id.Value))
            .OfType<CommandDefinition>()
            .Select(registry.Present)
            .ToList();

        var sections = new List<CommandPaletteSection>();
        if (recentItems.Count > 0) sections.Add(new CommandPaletteSection(texts["palette.recent"], recentItems));

        var shown = recentItems.Select(item => item.Command.Id.Value).ToHashSet(StringComparer.Ordinal);

        sections.AddRange(applicable
            .Where(command => !shown.Contains(command.Id.Value))
            .GroupBy(command => registry.CategoryKeyOf(command) ?? string.Empty)
            .OrderBy(group => group.Key.Length == 0 ? int.MaxValue : registry.MenuRootRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CommandPaletteSection(
                group.Key.Length == 0 ? texts["palette.other"] : texts[$"menu.{group.Key}"],
                [.. group.Select(registry.Present).OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)])));

        return sections;
    }

    public void Chosen(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        recent.Remember(command.Id);
    }
}
