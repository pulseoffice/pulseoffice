using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands;

public sealed record RibbonModel(IReadOnlyList<RibbonTab> Tabs, ModuleKind Module = ModuleKind.Shell)
{
    public static readonly RibbonModel Empty = new([]);

    public bool IsEmpty => Tabs.Count == 0;
}

public sealed record RibbonTab(string Title, IReadOnlyList<RibbonGroup> Groups)
{
    public bool IsContextual { get; init; }

    public string? AccentHex { get; init; }
}

public sealed record RibbonGroup(string Title, IReadOnlyList<RibbonItem> Items);

public sealed record RibbonItem(CommandDefinition Command, bool IsEnabled, string Title, string? Tooltip)
{
    public string? IconKey => Command.IconKey;

    public bool IsLarge => Title.Length <= 12;

    public bool? IsChecked { get; init; }

    public RibbonDisplay Display { get; init; } = RibbonDisplay.Auto;

    public RibbonControlSpec? Control => Command.RibbonControl;

    public string? Gesture { get; init; }

    public IReadOnlyList<RibbonItem>? Menu { get; init; }

    public bool IsStrip => Control is { FollowsDisplay: false } || Display == RibbonDisplay.Icon;
}
