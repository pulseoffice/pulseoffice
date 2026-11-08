using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets;

public sealed record NamedCellStyle
{
    public required string Name { get; init; }

    public required int StyleId { get; init; }

    public string? Parent { get; init; }

    public bool IsBuiltIn { get; init; }
}

public sealed class NamedStyleCatalog(StyleCatalog styles)
{
    private readonly StyleCatalog _styles = styles ?? throw new ArgumentNullException(nameof(styles));
    private readonly List<NamedCellStyle> _named = [];

    public IReadOnlyList<NamedCellStyle> All => _named;

    public NamedCellStyle Define(string name, CellStyle style, string? parent = null, bool builtIn = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(style);

        var entry = new NamedCellStyle
        {
            Name = name,
            StyleId = _styles.Intern(style),
            Parent = parent,
            IsBuiltIn = builtIn,
        };

        var existing = Find(name);

        if (existing is null) _named.Add(entry);
        else _named[_named.IndexOf(existing)] = entry;

        return entry;
    }

    public NamedCellStyle? Find(string name) =>
        _named.FirstOrDefault(style =>
            string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Restore(string name, NamedCellStyle? entry)
    {
        var existing = Find(name);
        if (existing is not null) _named.Remove(existing);
        if (entry is not null) _named.Add(entry);
    }

    public void ReplaceAll(IEnumerable<NamedCellStyle> entries)
    {
        _named.Clear();
        _named.AddRange(entries);
    }

    public bool Remove(string name)
    {
        if (Find(name) is not { IsBuiltIn: false } style) return false;

        _named.Remove(style);
        return true;
    }

    public CellStyle StyleOf(NamedCellStyle named)
    {
        ArgumentNullException.ThrowIfNull(named);

        var chain = new List<NamedCellStyle>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var current = named; current is not null && visited.Add(current.Name);
             current = current.Parent is { } parent ? Find(parent) : null)
            chain.Add(current);

        var style = _styles.Get(chain[^1].StyleId);
        for (var index = chain.Count - 2; index >= 0; index--)
            style = Merge(style, _styles.Get(chain[index].StyleId));
        return style;
    }

    private static CellStyle Merge(CellStyle parent, CellStyle child) => child with
    {
        Font = child.Font == CellFont.Default ? parent.Font : child.Font,
        BackgroundHex = child.BackgroundHex ?? parent.BackgroundHex,
        Borders = child.Borders.IsEmpty ? parent.Borders : child.Borders,
        Horizontal = child.Horizontal == CellHorizontalAlignment.General ? parent.Horizontal : child.Horizontal,
        NumberFormatId = child.NumberFormatId == 0 ? parent.NumberFormatId : child.NumberFormatId,
        Vertical = child.Vertical == CellStyle.Default.Vertical ? parent.Vertical : child.Vertical,
        WrapText = child.WrapText || parent.WrapText,
        RotationDegrees = child.RotationDegrees == 0 ? parent.RotationDegrees : child.RotationDegrees,
        Indent = child.Indent == 0 ? parent.Indent : child.Indent,
        Locked = child.Locked == CellStyle.Default.Locked ? parent.Locked : child.Locked,
        Language = child.Language ?? parent.Language,
    };

    public void AddDefaults()
    {
        Define("Default", CellStyle.Default, builtIn: true);

        Define("Heading", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, Size = 14 },
            Horizontal = CellHorizontalAlignment.Center,
        }, builtIn: true);

        Define("Heading 1", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, Size = 13 },
        }, parent: "Heading", builtIn: true);

        Define("Heading 2", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, Size = 12, Italic = true },
        }, parent: "Heading", builtIn: true);

        Define("Accent 1", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, ColorHex = "#FFFFFF" },
            BackgroundHex = "#2563EB",
        }, builtIn: true);

        Define("Accent 2", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, ColorHex = "#FFFFFF" },
            BackgroundHex = "#16A34A",
        }, builtIn: true);

        Define("Accent 3", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, ColorHex = "#FFFFFF" },
            BackgroundHex = "#DC2626",
        }, builtIn: true);

        Define("Good", CellStyle.Default with
        {
            Font = CellFont.Default with { ColorHex = "#0B6B33" },
            BackgroundHex = "#DCF3E4",
        }, builtIn: true);

        Define("Bad", CellStyle.Default with
        {
            Font = CellFont.Default with { ColorHex = "#8C1C13" },
            BackgroundHex = "#FBE3E1",
        }, builtIn: true);

        Define("Neutral", CellStyle.Default with
        {
            Font = CellFont.Default with { ColorHex = "#7A5A00" },
            BackgroundHex = "#FDF3D6",
        }, builtIn: true);

        Define("Error", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, ColorHex = "#FFFFFF" },
            BackgroundHex = "#DC2626",
        }, builtIn: true);

        Define("Warning", CellStyle.Default with
        {
            Font = CellFont.Default with { ColorHex = "#B45309" },
        }, builtIn: true);

        Define("Footnote", CellStyle.Default with
        {
            Font = CellFont.Default with { Size = 8, Italic = true, ColorHex = "#6B7280" },
        }, builtIn: true);

        Define("Note", CellStyle.Default with
        {
            BackgroundHex = "#FFF7CC",
            Borders = new CellBorders(
                new BorderEdge(BorderStyle.Thin, "#D9C56B", Foundation.Units.Twip.FromPoints(0.5)),
                new BorderEdge(BorderStyle.Thin, "#D9C56B", Foundation.Units.Twip.FromPoints(0.5)),
                new BorderEdge(BorderStyle.Thin, "#D9C56B", Foundation.Units.Twip.FromPoints(0.5)),
                new BorderEdge(BorderStyle.Thin, "#D9C56B", Foundation.Units.Twip.FromPoints(0.5))),
        }, builtIn: true);

        Define("Result", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, Underline = true },
        }, builtIn: true);
    }
}
