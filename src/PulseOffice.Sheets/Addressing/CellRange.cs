namespace PulseOffice.Sheets.Addressing;

public readonly record struct CellRange
{
    public CellRange(CellAddress a, CellAddress b)
    {
        Start = new CellAddress(Math.Min(a.Row, b.Row), Math.Min(a.Column, b.Column));
        End = new CellAddress(Math.Max(a.Row, b.Row), Math.Max(a.Column, b.Column));
    }

    public CellAddress Start { get; }
    public CellAddress End { get; }

    public static CellRange Single(CellAddress cell) => new(cell, cell);

    public int RowCount => End.Row - Start.Row + 1;
    public int ColumnCount => End.Column - Start.Column + 1;
    public long CellCount => (long)RowCount * ColumnCount;

    public bool IsWholeColumns => Start.Row == 0 && End.Row == CellAddress.MaxRows - 1;

    public bool IsWholeRows => Start.Column == 0 && End.Column == CellAddress.MaxColumns - 1;
    public bool IsSingleCell => Start == End;

    public bool Contains(CellAddress c) =>
        c.Row >= Start.Row && c.Row <= End.Row && c.Column >= Start.Column && c.Column <= End.Column;

    public bool IntersectsWith(CellRange o) =>
        Start.Row <= o.End.Row && End.Row >= o.Start.Row &&
        Start.Column <= o.End.Column && End.Column >= o.Start.Column;

    public IEnumerable<CellAddress> Cells()
    {
        for (var r = Start.Row; r <= End.Row; r++)
            for (var c = Start.Column; c <= End.Column; c++)
                yield return new CellAddress(r, c);
    }

    public static bool TryParseA1(ReadOnlySpan<char> text, out CellRange range)
    {
        range = default;
        var colon = text.IndexOf(':');
        if (colon < 0)
        {
            if (!CellAddress.TryParseA1(text, out var single)) return false;
            range = Single(single);
            return true;
        }
        if (!CellAddress.TryParseA1(text[..colon], out var a)) return false;
        if (!CellAddress.TryParseA1(text[(colon + 1)..], out var b)) return false;
        range = new CellRange(a, b);
        return true;
    }

    public override string ToString() => IsSingleCell ? Start.ToA1() : $"{Start.ToA1()}:{End.ToA1()}";
}
