using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI;

public interface ICellNavigator
{
    CellAddress Step(Worksheet sheet, CellAddress from, int rowDelta, int columnDelta);

    CellAddress Jump(Worksheet sheet, CellAddress from, int rowStep, int columnStep);

    CellAddress LastUsed(Worksheet sheet);

    CellAddress RowEnd(Worksheet sheet, CellAddress from);
}

public sealed class CellNavigator : ICellNavigator
{
    public CellAddress Step(Worksheet sheet, CellAddress from, int rowDelta, int columnDelta)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var current = from;

        for (var step = 0; step < Math.Abs(rowDelta); step++)
            current = Next(sheet, current, Math.Sign(rowDelta), 0);

        for (var step = 0; step < Math.Abs(columnDelta); step++)
            current = Next(sheet, current, 0, Math.Sign(columnDelta));

        return current;
    }

    public CellAddress Jump(Worksheet sheet, CellAddress from, int rowStep, int columnStep)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        rowStep = Math.Sign(rowStep);
        columnStep = Math.Sign(columnStep);
        if (rowStep == 0 && columnStep == 0) return from;

        var used = sheet.UsedRange();
        var candidates = Line(sheet, from, rowStep, columnStep, used).ToList();
        if (candidates.Count == 0) return Edge(sheet, from, rowStep, columnStep);

        if (HasContent(sheet, from) && HasContent(sheet, candidates[0]))
        {
            var last = candidates[0];
            foreach (var cell in candidates.Skip(1))
            {
                if (!HasContent(sheet, cell)) return last;
                last = cell;
            }
            return last;
        }

        foreach (var cell in candidates)
            if (HasContent(sheet, cell)) return cell;

        return Edge(sheet, from, rowStep, columnStep);
    }

    public CellAddress LastUsed(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.UsedRange() is { } used ? used.End : new CellAddress(0, 0);
    }

    public CellAddress RowEnd(Worksheet sheet, CellAddress from)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var last = -1;
        foreach (var (address, cell) in sheet.NonEmptyCells())
            if (address.Row == from.Row && HasContent(cell) && address.Column > last) last = address.Column;
        return last < 0 ? from : new CellAddress(from.Row, last);
    }

    private IEnumerable<CellAddress> Line(Worksheet sheet, CellAddress from, int rowStep, int columnStep, CellRange? used)
    {
        if (used is not { } bounds) yield break;

        var row = from.Row + rowStep;
        var column = from.Column + columnStep;
        while (Inside(row, column) && Within(row, column, rowStep, columnStep, bounds))
        {
            if (!Hidden(sheet, row, column, rowStep, columnStep)) yield return new CellAddress(row, column);
            row += rowStep;
            column += columnStep;
        }
    }

    private bool Within(int row, int column, int rowStep, int columnStep, CellRange bounds) =>
        (rowStep > 0 ? row <= bounds.End.Row : rowStep < 0 ? row >= bounds.Start.Row : true)
        && (columnStep > 0 ? column <= bounds.End.Column : columnStep < 0 ? column >= bounds.Start.Column : true);

    private CellAddress Edge(Worksheet sheet, CellAddress from, int rowStep, int columnStep)
    {
        var row = rowStep > 0 ? CellAddress.MaxRows - 1 : rowStep < 0 ? 0 : from.Row;
        var column = columnStep > 0 ? CellAddress.MaxColumns - 1 : columnStep < 0 ? 0 : from.Column;

        while (Inside(row, column) && Hidden(sheet, row, column, rowStep, columnStep) && (row != from.Row || column != from.Column))
        {
            row -= rowStep;
            column -= columnStep;
        }

        return new CellAddress(row, column);
    }

    private bool HasContent(Worksheet sheet, CellAddress address) => HasContent(sheet[address]);

    private bool HasContent(Cell cell) => !cell.Value.IsEmpty || cell.Formula is not null;

    private CellAddress Next(Worksheet sheet, CellAddress from, int rowStep, int columnStep)
    {
        var region = sheet.Merges.At(from);

        var row = rowStep switch
        {
            > 0 => (region?.End.Row ?? from.Row) + 1,
            < 0 => (region?.Start.Row ?? from.Row) - 1,
            _ => from.Row,
        };

        var column = columnStep switch
        {
            > 0 => (region?.End.Column ?? from.Column) + 1,
            < 0 => (region?.Start.Column ?? from.Column) - 1,
            _ => from.Column,
        };

        while (Inside(row, column) && Hidden(sheet, row, column, rowStep, columnStep))
        {
            row += rowStep;
            column += columnStep;
        }

        if (!Inside(row, column)) return from;

        var target = new CellAddress(row, column);

        return sheet.Merges.At(target) is { } landed && landed != region ? landed.Start : target;
    }

    private bool Hidden(Worksheet sheet, int row, int column, int rowStep, int columnStep) =>
        (rowStep != 0 && sheet.Layout.IsRowHidden(row)) || (columnStep != 0 && sheet.Layout.IsColumnHidden(column));

    private bool Inside(int row, int column) =>
        row >= 0 && column >= 0 && row < CellAddress.MaxRows && column < CellAddress.MaxColumns;
}
