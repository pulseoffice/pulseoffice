namespace PulseOffice.Presentations;

public sealed record LayoutDuplication(SlideMaster Master, SlideLayoutTemplate Copy);

public interface ISlideLayoutCatalog
{
    SlideLayoutTemplate? Find(SlideMaster master, string layoutId);

    bool IsStandard(SlideMaster master, SlideLayoutTemplate layout);

    SlideMaster Rename(SlideMaster master, string layoutId, string name);

    LayoutDuplication Duplicate(SlideMaster master, string layoutId, string name);

    SlideMaster Remove(SlideMaster master, string layoutId);
}

public sealed class SlideLayoutCatalog : ISlideLayoutCatalog
{
    public const string CustomPrefix = "layout.custom.";

    public SlideLayoutTemplate? Find(SlideMaster master, string layoutId)
    {
        ArgumentNullException.ThrowIfNull(master);
        return master.Layouts.FirstOrDefault(layout => string.Equals(layout.Id, layoutId, StringComparison.Ordinal));
    }

    public bool IsStandard(SlideMaster master, SlideLayoutTemplate layout)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(layout);
        return ReferenceEquals(master.Layouts.FirstOrDefault(candidate => candidate.Kind == layout.Kind), layout);
    }

    public SlideMaster Rename(SlideMaster master, string layoutId, string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0 || Find(master, layoutId) is not { } layout || IsStandard(master, layout) || layout.Name == trimmed) return master;
        return master with
        {
            Layouts = [.. master.Layouts.Select(candidate => ReferenceEquals(candidate, layout) ? candidate with { Name = trimmed } : candidate)],
        };
    }

    public LayoutDuplication Duplicate(SlideMaster master, string layoutId, string name)
    {
        var source = Find(master, layoutId)
            ?? throw new ArgumentException($"Layout '{layoutId}' is not in master '{master.Id}'.", nameof(layoutId));
        var trimmed = (name ?? string.Empty).Trim();
        var copy = source with { Id = NextCustomId(master), Name = trimmed.Length > 0 ? trimmed : source.Name };
        var layouts = master.Layouts.ToList();
        layouts.Insert(layouts.IndexOf(source) + 1, copy);
        return new LayoutDuplication(master with { Layouts = layouts }, copy);
    }

    public SlideMaster Remove(SlideMaster master, string layoutId)
    {
        if (Find(master, layoutId) is not { } layout || IsStandard(master, layout)) return master;
        return master with { Layouts = [.. master.Layouts.Where(candidate => !ReferenceEquals(candidate, layout))] };
    }

    private string NextCustomId(SlideMaster master)
    {
        var used = master.Layouts.Select(layout => layout.Id).ToHashSet(StringComparer.Ordinal);
        var ordinal = 1;
        while (used.Contains($"{CustomPrefix}{ordinal}")) ordinal++;
        return $"{CustomPrefix}{ordinal}";
    }
}
