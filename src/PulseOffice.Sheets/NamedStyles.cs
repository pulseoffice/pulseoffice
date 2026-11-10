using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets;

public sealed record NamedCellStyle
{
    public required string Name { get; init; }

    public required int StyleId { get; init; }

    public string? Parent { get; init; }

    public bool IsBuiltIn { get; init; }

    public CellStyleProperties Explicit { get; init; } = CellStyleProperties.All;

    public bool Sets(CellStyleProperties property) => (Explicit & property) == property;
}

public sealed class NamedStyleCatalog
{
    private readonly StyleCatalog _styles;
    private readonly ICellStyleLayering _layering;
    private readonly List<NamedCellStyle> _named = [];

    public NamedStyleCatalog(StyleCatalog styles) : this(styles, new CellStyleLayering())
    {
    }

    public NamedStyleCatalog(StyleCatalog styles, ICellStyleLayering layering)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        _layering = layering ?? throw new ArgumentNullException(nameof(layering));
    }

    public IReadOnlyList<NamedCellStyle> All => _named;

    public ICellStyleLayering Layering => _layering;

    public NamedCellStyle Define(string name, CellStyle style, string? parent = null, bool builtIn = false, CellStyleProperties? explicitProperties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(style);

        var properties = explicitProperties ?? _layering.Differences(style, CellStyle.Default);
        var entry = new NamedCellStyle
        {
            Name = name,
            StyleId = _styles.Intern(_layering.Overlay(CellStyle.Default, style, properties)),
            Parent = parent,
            IsBuiltIn = builtIn,
            Explicit = properties,
        };

        var index = IndexOf(name);

        if (index < 0) _named.Add(entry);
        else _named[index] = entry;

        return entry;
    }

    public NamedCellStyle Restyle(NamedCellStyle named, CellStyle own)
    {
        ArgumentNullException.ThrowIfNull(named);
        ArgumentNullException.ThrowIfNull(own);

        var previous = _styles.Get(named.StyleId);
        return Define(named.Name, own, named.Parent, named.IsBuiltIn, named.Explicit | _layering.Differences(own, previous));
    }

    public NamedCellStyle? Find(string name) =>
        _named.FirstOrDefault(style =>
            string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase));

    public int IndexOf(string name) =>
        _named.FindIndex(style => string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Restore(string name, NamedCellStyle? entry, int position = -1)
    {
        var index = IndexOf(name);
        if (index >= 0) _named.RemoveAt(index);
        if (entry is null) return;

        var target = index >= 0 ? index : position;
        if (target < 0 || target > _named.Count) _named.Add(entry);
        else _named.Insert(target, entry);
    }

    public void ReplaceAll(IEnumerable<NamedCellStyle> entries)
    {
        var list = entries.ToList();
        _named.Clear();
        _named.AddRange(list);
    }

    public bool Remove(string name)
    {
        if (Find(name) is not { IsBuiltIn: false } style) return false;

        ReplaceAll(Without(style.Name, Heir(style)));
        return true;
    }

    public string? Heir(NamedCellStyle removed)
    {
        ArgumentNullException.ThrowIfNull(removed);

        return removed.Parent is { } parent && !string.Equals(parent, removed.Name, StringComparison.OrdinalIgnoreCase) && Find(parent) is { } found
            ? found.Name
            : null;
    }

    public IReadOnlyList<NamedCellStyle> Without(string name, string? heir)
    {
        if (Find(name) is not { } removed) return [.. _named];

        var removedOwn = _styles.Get(removed.StyleId);
        return [.. _named.Where(entry => !ReferenceEquals(entry, removed)).Select(entry =>
            !string.Equals(entry.Parent, removed.Name, StringComparison.OrdinalIgnoreCase) ? entry
            : entry with
            {
                Parent = heir,
                Explicit = entry.Explicit | removed.Explicit,
                StyleId = _styles.Intern(_layering.Overlay(_layering.Overlay(CellStyle.Default, removedOwn, removed.Explicit), _styles.Get(entry.StyleId), entry.Explicit)),
            })];
    }

    public CellStyle OwnStyleOf(NamedCellStyle named)
    {
        ArgumentNullException.ThrowIfNull(named);
        return _styles.Get(named.StyleId);
    }

    public CellStyle InheritedStyleOf(NamedCellStyle named)
    {
        ArgumentNullException.ThrowIfNull(named);
        var chain = Chain(named);
        return Resolve(chain.Skip(1));
    }

    public CellStyleProperties DeclaredAlong(NamedCellStyle named)
    {
        ArgumentNullException.ThrowIfNull(named);
        return Chain(named).Aggregate(CellStyleProperties.None, (declared, entry) => declared | entry.Explicit);
    }

    public CellStyle StyleOf(NamedCellStyle named)
    {
        ArgumentNullException.ThrowIfNull(named);
        return Resolve(Chain(named));
    }

    private List<NamedCellStyle> Chain(NamedCellStyle named)
    {
        var chain = new List<NamedCellStyle>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var current = named; current is not null && visited.Add(current.Name);
             current = current.Parent is { } parent ? Find(parent) : null)
            chain.Add(current);
        return chain;
    }

    private CellStyle Resolve(IEnumerable<NamedCellStyle> chain)
    {
        var style = CellStyle.Default;
        foreach (var entry in chain.Reverse())
            style = _layering.Overlay(style, _styles.Get(entry.StyleId), entry.Explicit);
        return style;
    }

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
