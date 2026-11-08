namespace PulseOffice.Sheets.Engine.Parsing;

public sealed class SheetNameQuoting
{
    public string Quoted(string sheetName)
    {
        ArgumentNullException.ThrowIfNull(sheetName);

        return IsPlain(sheetName) ? sheetName : $"'{sheetName.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    public string Qualified(string sheetName, string reference) => $"{Quoted(sheetName)}.{reference}";

    private bool IsPlain(string sheetName) =>
        sheetName.Length > 0
        && !char.IsAsciiDigit(sheetName[0])
        && sheetName.All(character => char.IsLetterOrDigit(character) || character == '_');
}
