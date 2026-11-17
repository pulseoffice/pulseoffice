using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Shapes;

public enum ShapeSection
{
    Recent,
    Basic,
    Arrows,
    Stars,
    Callouts,
    Symbols,
    FlowChart,
    Lines,
}

public sealed record ShapeChoice(string Key, string TitleKey, ShapeSection Section, ShapeKind? Shape = null, string? IconKey = null);

public sealed record ShapeTool(string Key, string TitleKey, string IconKey, Action Run);

public interface IRecentShapes
{
    IReadOnlyList<string> Keys { get; }

    void Remember(string key);
}

public sealed class RecentShapes(int capacity = 12) : IRecentShapes
{
    private readonly List<string> _keys = [];

    public IReadOnlyList<string> Keys => [.. _keys];

    public void Remember(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _keys.Remove(key);
        _keys.Insert(0, key);
        if (_keys.Count > capacity) _keys.RemoveRange(capacity, _keys.Count - capacity);
    }
}

public sealed class ShapePickerModel
{
    private readonly ITextCatalog _texts;
    private readonly IReadOnlyList<ShapeChoice> _choices;
    private readonly IReadOnlyList<ShapeChoice> _recent;

    public ShapePickerModel(ITextCatalog texts, IEnumerable<ShapeTool> tools, IReadOnlyList<string> recentKeys)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(recentKeys);
        _texts = texts;

        _choices =
        [
            .. Family(ShapeSection.Basic, ShapeFamilies.Basic),
            .. Family(ShapeSection.Arrows, ShapeFamilies.Arrows),
            .. Family(ShapeSection.Stars, ShapeFamilies.Stars),
            .. Family(ShapeSection.Callouts, ShapeFamilies.Callouts),
            .. Family(ShapeSection.Symbols, ShapeFamilies.Symbols),
            .. Family(ShapeSection.FlowChart, ShapeFamilies.FlowChart),
            .. Family(ShapeSection.Lines, [ShapeKind.Line]),
            .. tools.Select(tool => new ShapeChoice(tool.Key, tool.TitleKey, ShapeSection.Lines, IconKey: tool.IconKey)),
        ];

        _recent = [.. recentKeys.Select(Find).OfType<ShapeChoice>().Select(choice => choice with { Section = ShapeSection.Recent })];
    }

    public static string KeyOf(ShapeKind kind) => $"shape.{ShapeGalleryView.Key(kind)}";

    public IReadOnlyList<ShapeSection> Sections =>
    [
        .. _recent.Count > 0 ? [ShapeSection.Recent] : Array.Empty<ShapeSection>(),
        .. Enum.GetValues<ShapeSection>().Where(section => section != ShapeSection.Recent && _choices.Any(choice => choice.Section == section)),
    ];

    public string TitleOf(ShapeSection section) => _texts[$"shape.picker.section.{SectionKey(section)}"];

    public string TitleOf(ShapeChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return _texts[choice.TitleKey];
    }

    public int CountOf(ShapeSection section) => Items(section, string.Empty).Count;

    public ShapeChoice? Find(string key) => _choices.FirstOrDefault(choice => choice.Key == key);

    public IReadOnlyList<ShapeChoice> Items(ShapeSection? section, string query)
    {
        var filter = (query ?? string.Empty).Trim();
        var source = section switch
        {
            ShapeSection.Recent => _recent,
            { } chosen => _choices.Where(choice => choice.Section == chosen).ToList(),
            null => _choices.DistinctBy(choice => choice.Key).ToList(),
        };
        return [.. source.Where(choice => Matches(choice, filter))];
    }

    private bool Matches(ShapeChoice choice, string filter) =>
        filter.Length == 0
        || Contains(TitleOf(choice), filter)
        || Contains(TitleOf(choice.Section), filter);

    private static bool Contains(string text, string filter) =>
        CultureInfo.CurrentCulture.CompareInfo.IndexOf(text, filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private static IEnumerable<ShapeChoice> Family(ShapeSection section, IReadOnlyList<ShapeKind> kinds) =>
        kinds.Select(kind => new ShapeChoice(KeyOf(kind), KeyOf(kind), section, kind));

    private static string SectionKey(ShapeSection section) =>
        char.ToLowerInvariant(section.ToString()[0]) + section.ToString()[1..];
}
