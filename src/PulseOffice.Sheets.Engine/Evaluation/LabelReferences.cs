using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation;

public interface ILabelReferences
{
    CellRange? Find(Worksheet sheet, string label, bool automatic);
}

public sealed class LabelReferences : ILabelReferences
{
    private const int SearchLimit = 1_000_000;

    public CellRange? Find(Worksheet sheet, string label, bool automatic)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Defined(sheet, label) ?? (automatic ? Automatic(sheet, label) : null);
    }

    private CellRange? Defined(Worksheet sheet, string label)
    {
        foreach (var range in sheet.Labels.All)
            foreach (var address in range.Labels.Cells())
            {
                if (!Matches(sheet[address].Value, label)) continue;
                return range.Orientation == LabelOrientation.Columns
                    ? new CellRange(new CellAddress(range.Data.Start.Row, address.Column), new CellAddress(range.Data.End.Row, address.Column))
                    : new CellRange(new CellAddress(address.Row, range.Data.Start.Column), new CellAddress(address.Row, range.Data.End.Column));
            }
        return null;
    }

    private CellRange? Automatic(Worksheet sheet, string label)
    {
        var found = sheet.NonEmptyCells().Take(SearchLimit)
            .Where(pair => Matches(pair.Value.Value, label))
            .Select(pair => pair.Key)
            .OrderBy(address => address.Row).ThenBy(address => address.Column)
            .Cast<CellAddress?>()
            .FirstOrDefault();
        if (found is not { } cell) return null;

        var below = new CellAddress(cell.Row + 1, cell.Column);
        if (below.Row < CellAddress.MaxRows && !sheet[below].Value.IsEmpty)
        {
            var end = below;
            while (end.Row + 1 < CellAddress.MaxRows && !sheet[new CellAddress(end.Row + 1, cell.Column)].Value.IsEmpty) end = new CellAddress(end.Row + 1, cell.Column);
            return new CellRange(below, end);
        }

        var right = new CellAddress(cell.Row, cell.Column + 1);
        if (right.Column < CellAddress.MaxColumns && !sheet[right].Value.IsEmpty)
        {
            var end = right;
            while (end.Column + 1 < CellAddress.MaxColumns && !sheet[new CellAddress(cell.Row, end.Column + 1)].Value.IsEmpty) end = new CellAddress(cell.Row, end.Column + 1);
            return new CellRange(right, end);
        }
        return null;
    }

    private bool Matches(CellValue value, string label) =>
        value.Kind == ValueKind.Text && string.Equals(value.AsText().Trim(), label, StringComparison.CurrentCultureIgnoreCase);
}
