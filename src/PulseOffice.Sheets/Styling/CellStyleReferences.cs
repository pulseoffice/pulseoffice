using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Styling;

public enum CellStyleRenameCheck
{
    Allowed,
    Unchanged,
    Missing,
    BuiltIn,
    Taken,
    Invalid,
}

public sealed record CellStyleLink(Worksheet Sheet, CellAddress Address, string? Before, string? After);

public sealed record CellStyleRelink(IReadOnlyList<NamedCellStyle> Before, IReadOnlyList<NamedCellStyle> After, IReadOnlyList<CellStyleLink> Cells);

public interface ICellStyleReferences
{
    int MaximumNameLength { get; }

    CellStyleRenameCheck CheckRename(Workbook workbook, string name, string newName);

    IReadOnlyList<Worksheet> SheetsUsing(Workbook workbook, string name);

    CellStyleRelink? Rename(Workbook workbook, string name, string newName);

    CellStyleRelink? Delete(Workbook workbook, string name);

    void Apply(Workbook workbook, CellStyleRelink relink);

    void Revert(Workbook workbook, CellStyleRelink relink);
}

public sealed class CellStyleReferences : ICellStyleReferences
{
    public int MaximumNameLength => 255;

    public CellStyleRenameCheck CheckRename(Workbook workbook, string name, string newName)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var catalog = workbook.NamedStyles;
        if (catalog.Find(name) is not { } style) return CellStyleRenameCheck.Missing;
        if (style.IsBuiltIn) return CellStyleRenameCheck.BuiltIn;
        var target = (newName ?? string.Empty).Trim();
        if (target.Length == 0 || target.Length > MaximumNameLength || target.Any(char.IsControl)) return CellStyleRenameCheck.Invalid;
        if (string.Equals(target, style.Name, StringComparison.Ordinal)) return CellStyleRenameCheck.Unchanged;
        return catalog.Find(target) is { } other && !ReferenceEquals(other, style) ? CellStyleRenameCheck.Taken : CellStyleRenameCheck.Allowed;
    }

    public IReadOnlyList<Worksheet> SheetsUsing(Workbook workbook, string name)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return [.. workbook.Sheets.Where(sheet => sheet.NonEmptyCells().Any(pair => Names(pair.Value.StyleName, name)))];
    }

    public CellStyleRelink? Rename(Workbook workbook, string name, string newName)
    {
        if (CheckRename(workbook, name, newName) != CellStyleRenameCheck.Allowed) return null;
        var catalog = workbook.NamedStyles;
        var style = catalog.Find(name)!;
        var target = newName.Trim();
        var before = catalog.All.ToList();
        var after = before.Select(entry => ReferenceEquals(entry, style)
            ? entry with { Name = target, Parent = Names(entry.Parent, style.Name) ? target : entry.Parent }
            : Names(entry.Parent, style.Name) ? entry with { Parent = target } : entry).ToList();
        var relink = new CellStyleRelink(before, after, Links(workbook, style.Name, target));
        Apply(workbook, relink);
        return relink;
    }

    public CellStyleRelink? Delete(Workbook workbook, string name)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var catalog = workbook.NamedStyles;
        if (catalog.Find(name) is not { IsBuiltIn: false } style) return null;
        var heir = catalog.Heir(style);
        var before = catalog.All.ToList();
        var after = catalog.Without(style.Name, heir);
        var relink = new CellStyleRelink(before, after, Links(workbook, style.Name, heir));
        Apply(workbook, relink);
        return relink;
    }

    public void Apply(Workbook workbook, CellStyleRelink relink)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(relink);
        workbook.NamedStyles.ReplaceAll(relink.After);
        foreach (var link in relink.Cells) link.Sheet.SetStyleName(link.Address, link.After);
    }

    public void Revert(Workbook workbook, CellStyleRelink relink)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(relink);
        workbook.NamedStyles.ReplaceAll(relink.Before);
        foreach (var link in relink.Cells) link.Sheet.SetStyleName(link.Address, link.Before);
    }

    private List<CellStyleLink> Links(Workbook workbook, string name, string? target) =>
        [.. workbook.Sheets.SelectMany(sheet => sheet.NonEmptyCells()
            .Where(pair => Names(pair.Value.StyleName, name))
            .Select(pair => new CellStyleLink(sheet, pair.Key, pair.Value.StyleName, target)))];

    private bool Names(string? reference, string name) => string.Equals(reference, name, StringComparison.OrdinalIgnoreCase);
}
