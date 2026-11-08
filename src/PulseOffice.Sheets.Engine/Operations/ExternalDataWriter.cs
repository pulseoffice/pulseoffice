using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IExternalDataWriter
{
    CellRange Write(Worksheet sheet, CellAddress anchor, IReadOnlyList<IReadOnlyList<string>> rows, CellRange? previous);

    void WriteStream(Worksheet sheet, CellRange range, IReadOnlyList<IReadOnlyList<string>> lines, StreamInsertion insertion);
}

public sealed class ExternalDataWriter(IExternalCellValues values) : IExternalDataWriter
{
    public const int MaximumCells = 1_000_000;

    public CellRange Write(Worksheet sheet, CellAddress anchor, IReadOnlyList<IReadOnlyList<string>> rows, CellRange? previous)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rows);

        var rowCount = Math.Max(rows.Count, 1);
        var columnCount = Math.Max(rows.Count == 0 ? 1 : rows.Max(row => row.Count), 1);
        if ((long)rowCount * columnCount > MaximumCells)
            throw new InvalidOperationException("The external data exceeds the cell limit.");

        var area = new CellRange(anchor, new CellAddress(
            Math.Min(anchor.Row + rowCount - 1, CellAddress.MaxRows - 1), Math.Min(anchor.Column + columnCount - 1, CellAddress.MaxColumns - 1)));

        if (previous is { } old) Clear(sheet, old);
        Clear(sheet, area);

        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < rows[r].Count; c++)
            {
                var address = new CellAddress(anchor.Row + r, anchor.Column + c);
                if (!area.Contains(address)) continue;
                sheet.SetValue(address, values.Parse(rows[r][c]));
            }

        return area;
    }

    public void WriteStream(Worksheet sheet, CellRange range, IReadOnlyList<IReadOnlyList<string>> lines, StreamInsertion insertion)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(lines);

        Clear(sheet, range);

        var height = range.End.Row - range.Start.Row + 1;
        var width = range.End.Column - range.Start.Column + 1;
        var latest = lines.Skip(Math.Max(lines.Count - height, 0)).ToList();
        if (insertion == StreamInsertion.Top) latest.Reverse();

        var top = insertion == StreamInsertion.Bottom ? range.End.Row - latest.Count + 1 : range.Start.Row;

        for (var r = 0; r < latest.Count; r++)
            for (var c = 0; c < Math.Min(width, latest[r].Count); c++)
                sheet.SetValue(new CellAddress(top + r, range.Start.Column + c), values.Parse(latest[r][c]));
    }

    private void Clear(Worksheet sheet, CellRange range)
    {
        foreach (var (address, _) in sheet.NonEmptyCellsIn(range).ToList())
            sheet.SetValue(address, CellValue.Empty);
    }
}
