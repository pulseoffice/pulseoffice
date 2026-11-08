using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IDefinedNameRules
{
    bool IsValid(string name);

    string Sanitised(string label);

    string Suggested(Worksheet sheet, Addressing.CellRange selection);
}

public sealed class DefinedNameRules : IDefinedNameRules
{
    public bool IsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!(char.IsLetter(name[0]) || name[0] == '_')) return false;
        if (!name.All(character => char.IsLetterOrDigit(character) || character is '_' or '.')) return false;
        if (CellAddress.TryParseA1(name, out _)) return false;

        return !string.Equals(name, "TRUE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, "FALSE", StringComparison.OrdinalIgnoreCase);
    }

    public string Sanitised(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var cleaned = new string([.. label.Trim().Select(character =>
            char.IsLetterOrDigit(character) || character == '_' ? character : '_')]);

        if (cleaned.Length == 0) return cleaned;
        if (!(char.IsLetter(cleaned[0]) || cleaned[0] == '_')) cleaned = "_" + cleaned;

        return IsValid(cleaned) ? cleaned : "_" + cleaned;
    }

    private const int LabelReach = 4;

    public string Suggested(Worksheet sheet, Addressing.CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var wide = selection.ColumnCount > selection.RowCount;
        var label = wide ? Before(sheet, selection.Start, row: false) ?? Before(sheet, selection.Start, row: true)
            : Before(sheet, selection.Start, row: true) ?? Before(sheet, selection.Start, row: false);
        return label is null ? string.Empty : Sanitised(label);
    }

    private string? Before(Worksheet sheet, Addressing.CellAddress start, bool row)
    {
        for (var step = 1; step <= LabelReach; step++)
        {
            var address = row ? new Addressing.CellAddress(start.Row - step, start.Column) : new Addressing.CellAddress(start.Row, start.Column - step);
            if (!address.IsValid) return null;
            var value = sheet[address].Value;
            if (value.IsEmpty) continue;
            return value.Kind == Values.ValueKind.Text ? value.AsText() : null;
        }
        return null;
    }
}
