using PulseOffice.Foundation.Pages;

namespace PulseOffice.Sheets;

public sealed record PageStyle
{
    public required string Name { get; init; }

    public required PageSetup Setup { get; init; }

    public bool IsBuiltIn { get; init; }
}

public sealed class PageStyleCatalog
{
    private readonly List<PageStyle> _styles = [];

    public PageStyleCatalog(IPaperCatalog? papers = null)
    {
        var catalog = papers ?? new PaperCatalog();

        _styles.Add(new PageStyle
        {
            Name = "Default",
            IsBuiltIn = true,
            Setup = new PageSetup
            {
                Paper = catalog.Default,
                Footer = new HeaderFooter(Center: "&P / &N"),
                Header = new HeaderFooter(Left: "&F", Right: "&A"),
            },
        });
    }

    public IReadOnlyList<PageStyle> All => _styles;

    public PageStyle Default => _styles[0];

    public PageStyle Define(string name, PageSetup setup, bool builtIn = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(setup);

        var style = new PageStyle { Name = name, Setup = setup, IsBuiltIn = builtIn };
        var existing = Find(name);

        if (existing is null) _styles.Add(style);
        else _styles[_styles.IndexOf(existing)] = style;

        return style;
    }

    public void CopyTo(PageStyleCatalog target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target._styles.Clear();
        target._styles.AddRange(_styles);
    }

    public bool Remove(string name)
    {
        var existing = Find(name);
        if (existing is null || existing.IsBuiltIn || ReferenceEquals(existing, Default)) return false;

        return _styles.Remove(existing);
    }

    public PageStyle? Find(string name) =>
        _styles.FirstOrDefault(style =>
            string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase));

    public PageStyle For(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return Find(sheet.PageStyleName) ?? Default;
    }

    public string OwnNameFor(Worksheet sheet, IEnumerable<Worksheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(sheets);

        var others = sheets.Where(other => !ReferenceEquals(other, sheet)).ToList();
        var current = Find(sheet.PageStyleName);

        if (current is { IsBuiltIn: false }
            && !others.Any(other => string.Equals(other.PageStyleName, current.Name, StringComparison.OrdinalIgnoreCase)))
            return current.Name;

        for (var number = 1; ; number++)
        {
            var name = "PageStyle" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (Find(name) is null
                && !others.Any(other => string.Equals(other.PageStyleName, name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
    }
}
