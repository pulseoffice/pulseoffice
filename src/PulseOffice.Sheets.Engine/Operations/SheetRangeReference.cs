using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SheetRangeReference(int SheetIndex, CellRange Range);

public interface ISheetRangeReferences
{
    SheetRangeReference? Parse(Workbook workbook, string text, int currentSheet);

    string Qualify(Workbook workbook, int sheetIndex, string address, int currentSheet);
}

public sealed class SheetRangeReferences : ISheetRangeReferences
{
    public SheetRangeReference? Parse(Workbook workbook, string text, int currentSheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var trimmed = (text ?? string.Empty).Trim().TrimStart('$');
        if (trimmed.Length == 0) return null;

        var split = Separator(trimmed);
        if (split < 0) return Range(currentSheet, trimmed);

        var name = trimmed[..split].Trim().TrimStart('$');
        if (name.Length >= 2 && name[0] == '\'' && name[^1] == '\'')
            name = name[1..^1].Replace("''", "'", StringComparison.Ordinal);

        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (string.Equals(workbook.Sheets[index].Name, name, StringComparison.CurrentCultureIgnoreCase))
                return Range(index, Strip(trimmed[(split + 1)..]));

        return null;
    }

    public string Qualify(Workbook workbook, int sheetIndex, string address, int currentSheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(address);

        if (sheetIndex == currentSheet || sheetIndex < 0 || sheetIndex >= workbook.Sheets.Count) return address;

        return _quoting.Qualified(workbook.Sheets[sheetIndex].Name, address);
    }

    private readonly Parsing.SheetNameQuoting _quoting = new();

    private string Strip(string address)
    {
        var parts = address.Split(':');
        return string.Join(':', parts.Select(part =>
        {
            var dot = Separator(part);
            return dot >= 0 ? part[(dot + 1)..] : part;
        }));
    }

    private SheetRangeReference? Range(int sheet, string address)
    {
        var clean = address.Replace("$", string.Empty, StringComparison.Ordinal);
        if (CellRange.TryParseA1(clean, out var range)) return new SheetRangeReference(sheet, range);
        if (CellAddress.TryParseA1(clean, out var cell)) return new SheetRangeReference(sheet, new CellRange(cell, cell));
        return null;
    }

    private int Separator(string text)
    {
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\'') quoted = !quoted;
            else if (!quoted && (text[index] == '.' || text[index] == '!')) return index;
        }

        return -1;
    }
}
