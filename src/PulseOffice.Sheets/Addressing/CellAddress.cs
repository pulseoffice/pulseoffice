using System.Globalization;

namespace PulseOffice.Sheets.Addressing;

public readonly record struct CellAddress(int Row, int Column) : IComparable<CellAddress>
{
    public const int MaxRows = 1_048_576;
    public const int MaxColumns = 16_384;

    public static readonly CellAddress First = new(0, 0);

    public bool IsValid => Row >= 0 && Row < MaxRows && Column >= 0 && Column < MaxColumns;

    public static string ColumnName(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        Span<char> buffer = stackalloc char[4];
        var i = buffer.Length;
        var n = column;
        do
        {
            buffer[--i] = (char)('A' + n % 26);
            n = n / 26 - 1;
        } while (n >= 0);
        return new string(buffer[i..]);
    }

    public static int ParseColumn(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty || name.Length > 3) return -1;
        var value = 0;
        foreach (var ch in name)
        {
            var upper = char.ToUpperInvariant(ch);
            if (upper is < 'A' or > 'Z') return -1;
            value = value * 26 + (upper - 'A' + 1);
        }
        return value - 1;
    }

    public string ToA1(bool absoluteColumn = false, bool absoluteRow = false) =>
    string.Concat(
        absoluteColumn ? "$" : "", ColumnName(Column),
        absoluteRow ? "$" : "", (Row + 1).ToString(CultureInfo.InvariantCulture));

    public static bool TryParseA1(ReadOnlySpan<char> text, out CellAddress address)
    {
        address = default;
        text = text.Trim();
        if (text.IsEmpty) return false;
        if (text[0] == '$') text = text[1..];

        var split = 0;
        while (split < text.Length && char.IsAsciiLetter(text[split])) split++;
        if (split == 0) return false;

        var column = ParseColumn(text[..split]);
        if (column < 0) return false;

        var rowPart = text[split..];
        if (!rowPart.IsEmpty && rowPart[0] == '$') rowPart = rowPart[1..];
        if (!int.TryParse(rowPart, NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row < 1)
            return false;

        address = new CellAddress(row - 1, column);
        return address.IsValid;
    }

    public CellAddress Offset(int rows, int columns) => new(Row + rows, Column + columns);

    public int CompareTo(CellAddress other)
    {
        var byRow = Row.CompareTo(other.Row);
        return byRow != 0 ? byRow : Column.CompareTo(other.Column);
    }

    public override string ToString() => ToA1();
}
