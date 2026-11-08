using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI;

public readonly record struct NameBoxTarget(int SheetIndex, CellRange Range);

public interface INameBoxResolver
{
    NameBoxTarget? Resolve(string text, Workbook workbook, int currentSheet);
}

public sealed class NameBoxResolver : INameBoxResolver
{
    private const int MaxNameDepth = 8;

    public NameBoxTarget? Resolve(string text, Workbook workbook, int currentSheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (currentSheet < 0 || currentSheet >= workbook.Sheets.Count) return null;
        return Resolve(text.Trim(), workbook, currentSheet, 0);
    }

    private NameBoxTarget? Resolve(string text, Workbook workbook, int currentSheet, int depth)
    {
        if (text.StartsWith('=')) text = text[1..].Trim();
        if (text.Length == 0 || depth > MaxNameDepth) return null;

        if (Reference(text, workbook, currentSheet) is { } reference) return reference;

        var sheetName = workbook.Sheets[currentSheet].Name;
        var name = workbook.ResolveName(text, sheetName);
        if (name is null) return null;

        var scopeSheet = name.Scope is null ? currentSheet : IndexOf(workbook, name.Scope);
        return scopeSheet < 0 ? null : Resolve(name.Expression.Trim(), workbook, scopeSheet, depth + 1);
    }

    private NameBoxTarget? Reference(string text, Workbook workbook, int currentSheet)
    {
        var sheet = currentSheet;
        var split = SheetSeparator(text);
        if (split > 0)
        {
            var name = text[..split];
            if (name.StartsWith('$')) name = name[1..];
            if (name.Length >= 2 && name[0] == '\'' && name[^1] == '\'') name = name[1..^1].Replace("''", "'", StringComparison.Ordinal);
            sheet = IndexOf(workbook, name);
            if (sheet < 0) return null;
            text = text[(split + 1)..];
        }

        var parts = text.Split(':');
        if (parts.Length == 2)
        {
            var second = parts[1];
            var secondSplit = SheetSeparator(second);
            if (secondSplit > 0) parts[1] = second[(secondSplit + 1)..];
        }

        return CellRange.TryParseA1(string.Join(':', parts), out var range) ? new NameBoxTarget(sheet, range) : null;
    }

    private int SheetSeparator(string text)
    {
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\'') quoted = !quoted;
            else if (!quoted && text[i] is '.' or '!') return i;
        }
        return -1;
    }

    private int IndexOf(Workbook workbook, string sheetName)
    {
        for (var i = 0; i < workbook.Sheets.Count; i++)
            if (string.Equals(workbook.Sheets[i].Name, sheetName, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
