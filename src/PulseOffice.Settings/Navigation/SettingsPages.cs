using PulseOffice.Foundation.Modules;

namespace PulseOffice.Settings.Navigation;

public sealed record SettingsSection(string? Id, IReadOnlyList<SettingDefinition> Settings)
{
    public string? TitleKey => Id is null ? null : SettingCategoryPath.TitleKeyOf(Id);
}

public sealed record SettingsPage(string Id, ModuleKind Module, IReadOnlyList<SettingsSection> Sections)
{
    public bool IsModule => Module != ModuleKind.Shell;

    public string TitleKey => SettingCategoryPath.TitleKeyOf(Id);

    public string IconKey => $"settings.page.{Id}";

    public int Count => Sections.Sum(section => section.Settings.Count);

    public IEnumerable<SettingDefinition> Settings => Sections.SelectMany(section => section.Settings);
}

public sealed record SettingCategoryPath(string Page, string? Section)
{
    public static SettingCategoryPath Of(SettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var segments = definition.Category.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var page = definition.Module == ModuleKind.Shell
            ? segments.FirstOrDefault() ?? "general"
            : $"module.{definition.Module.ToString().ToLowerInvariant()}";
        var rest = definition.Module == ModuleKind.Shell ? segments.Skip(1) : segments;

        return new SettingCategoryPath(page, rest.LastOrDefault());
    }

    public static string TitleKeyOf(string segment) => $"settingCategory.{segment}";

    public IEnumerable<string> Segments => Section is null ? [Page] : [Page, Section];
}

public interface ISettingsPageBuilder
{
    IReadOnlyList<SettingsPage> Build(IEnumerable<SettingDefinition> definitions);
}

public sealed class SettingsPageBuilder : ISettingsPageBuilder
{
    public IReadOnlyList<SettingsPage> Build(IEnumerable<SettingDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var placed = definitions
            .Select((definition, order) => (Definition: definition, Path: SettingCategoryPath.Of(definition), Order: order))
            .ToList();

        return placed
            .GroupBy(entry => (entry.Path.Page, entry.Definition.Module))
            .OrderBy(group => group.Key.Module == ModuleKind.Shell ? 0 : 1)
            .ThenBy(group => group.Key.Module)
            .ThenBy(group => group.Min(entry => entry.Order))
            .Select(group => new SettingsPage(group.Key.Page, group.Key.Module, Sections(group)))
            .ToList();
    }

    private IReadOnlyList<SettingsSection> Sections(IEnumerable<(SettingDefinition Definition, SettingCategoryPath Path, int Order)> entries) =>
        entries
            .GroupBy(entry => entry.Path.Section)
            .OrderBy(group => group.Key is null ? 0 : 1)
            .ThenBy(group => group.Min(entry => entry.Order))
            .Select(group => new SettingsSection(
                group.Key,
                group.OrderBy(entry => entry.Order).Select(entry => entry.Definition).ToList()))
            .ToList();
}

public sealed record SettingsQuery(string Text, bool IncludeAdvanced)
{
    public bool IsSearch => !string.IsNullOrWhiteSpace(Text);
}

public interface ISettingsNavigator
{
    IReadOnlyList<SettingsPage> Pages(SettingsQuery query);

    bool HasAdvanced { get; }
}

public sealed class SettingsNavigator(ISettingsService settings, ISettingsPageBuilder pages) : ISettingsNavigator
{
    private readonly ISettingsService _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ISettingsPageBuilder _pages = pages ?? throw new ArgumentNullException(nameof(pages));

    public bool HasAdvanced => _settings.Definitions.Any(definition => definition.IsAdvanced);

    public IReadOnlyList<SettingsPage> Pages(SettingsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var matching = _settings.Search(query.Text).ToHashSet();
        var visible = _settings.Definitions
            .Where(definition => matching.Contains(definition))
            .Where(definition => query.IncludeAdvanced || query.IsSearch || !definition.IsAdvanced);

        return _pages.Build(visible);
    }
}
