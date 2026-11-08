namespace PulseOffice.Sheets.Addressing;

public sealed record SheetQualifiedReference(string? Sheet, string Local);

public sealed class SheetReferenceText
{
    public SheetQualifiedReference Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        var bang = trimmed.LastIndexOf('!');
        var local = (bang >= 0 ? trimmed[(bang + 1)..] : trimmed).Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        if (bang <= 0) return new SheetQualifiedReference(null, local);
        var sheet = trimmed[..bang].Trim();
        if (sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\'')
            sheet = sheet[1..^1].Replace("''", "'", StringComparison.Ordinal);
        return new SheetQualifiedReference(sheet.Length == 0 ? null : sheet, local);
    }

    public string Join(string? sheet, string local) =>
        string.IsNullOrEmpty(sheet) ? local : $"{Quoted(sheet)}!{local}";

    public string? Qualify(string? sheet, string? ownSheet, string? local)
    {
        if (string.IsNullOrWhiteSpace(local)) return null;
        return string.IsNullOrEmpty(sheet) || string.Equals(sheet, ownSheet, StringComparison.OrdinalIgnoreCase) ? local : Join(sheet, local);
    }

    private string Quoted(string sheet) =>
        sheet.All(c => char.IsLetterOrDigit(c) || c == '_') && !char.IsDigit(sheet[0])
            ? sheet
            : $"'{sheet.Replace("'", "''", StringComparison.Ordinal)}'";
}
