namespace PulseOffice.UI.Controls.StylePanels;

public sealed record StyleFamilyChoice(string Key, string Title)
{
    public string? Icon { get; init; }

    public bool TracksUsage { get; init; }
}

public sealed record StylePreview
{
    public string? Font { get; init; }

    public double? Size { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public string? Foreground { get; init; }

    public string? Background { get; init; }

    public string? Swatch { get; init; }

    public string? SwatchBorder { get; init; }
}

public sealed record StyleListEntry(string Name, string Title, int Depth)
{
    public StylePreview? Preview { get; init; }

    public bool Custom { get; init; }

    public bool InUse { get; init; }
}

public enum StylePanelAction
{
    New,
    Derive,
    Update,
    Edit,
    Delete,
    Rename,
}

public interface IStylesPanelSource
{
    IReadOnlyList<StyleFamilyChoice> Families { get; }

    IReadOnlyList<StyleListEntry> Styles(string family);

    string? Current(string family);

    bool CanApply(string family);

    bool Can(StylePanelAction action, string family, string? style);

    void Apply(string family, string style);

    Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct);

    event EventHandler? Changed;
}
