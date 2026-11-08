using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Operations;

public enum CellShift { Right, Down, EntireRow, EntireColumn }

public interface ISheetCellShift
{
    bool CanInsert(Worksheet sheet, CellRange range, CellShift shift);

    bool KeepsBlocksWhole(Workbook workbook, int sheetIndex, CellRange range, CellShift shift, bool insert);

    void Insert(CalculationEngine engine, int sheetIndex, CellRange range, CellShift shift);

    void Delete(CalculationEngine engine, int sheetIndex, CellRange range, CellShift shift);
}

public sealed class SheetCellShift(ISheetStructure structure) : ISheetCellShift
{
    public bool CanInsert(Worksheet sheet, CellRange range, CellShift shift)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var spill = shift switch
        {
            CellShift.Down => new CellRange(
                new CellAddress(CellAddress.MaxRows - range.RowCount, range.Start.Column),
                new CellAddress(CellAddress.MaxRows - 1, range.End.Column)),
            CellShift.Right => new CellRange(
                new CellAddress(range.Start.Row, CellAddress.MaxColumns - range.ColumnCount),
                new CellAddress(range.End.Row, CellAddress.MaxColumns - 1)),
            CellShift.EntireRow => new CellRange(
                new CellAddress(CellAddress.MaxRows - range.RowCount, 0),
                new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1)),
            _ => new CellRange(
                new CellAddress(0, CellAddress.MaxColumns - range.ColumnCount),
                new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1)),
        };

        return !sheet.NonEmptyCellsIn(spill).Any();
    }

    public void Insert(CalculationEngine engine, int sheetIndex, CellRange range, CellShift shift)
    {
        ArgumentNullException.ThrowIfNull(engine);

        switch (shift)
        {
            case CellShift.EntireRow:
                structure.InsertRows(engine, sheetIndex, range.Start.Row, range.RowCount);
                return;

            case CellShift.EntireColumn:
                structure.InsertColumns(engine, sheetIndex, range.Start.Column, range.ColumnCount);
                return;
        }

        Move(engine, sheetIndex, range, shift, insert: true);
    }

    public void Delete(CalculationEngine engine, int sheetIndex, CellRange range, CellShift shift)
    {
        ArgumentNullException.ThrowIfNull(engine);

        switch (shift)
        {
            case CellShift.EntireRow:
                structure.DeleteRows(engine, sheetIndex, range.Start.Row, range.RowCount);
                return;

            case CellShift.EntireColumn:
                structure.DeleteColumns(engine, sheetIndex, range.Start.Column, range.ColumnCount);
                return;
        }

        Move(engine, sheetIndex, range, shift, insert: false);
    }

    public bool KeepsBlocksWhole(Workbook workbook, int sheetIndex, CellRange range, CellShift shift, bool insert)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return shift is CellShift.EntireRow or CellShift.EntireColumn
            || !structure.SplitsBlocks(workbook, sheetIndex, Change(workbook.Sheets[sheetIndex], range, shift == CellShift.Down, insert));
    }

    private void Move(CalculationEngine engine, int sheetIndex, CellRange range, CellShift shift, bool insert) =>
        structure.ShiftBand(engine, sheetIndex, Change(engine.Workbook.Sheets[sheetIndex], range, shift == CellShift.Down, insert));

    private StructureChange Change(Worksheet sheet, CellRange range, bool down, bool insert) =>
        new(
            down ? StructureAxis.Rows : StructureAxis.Columns,
            down ? range.Start.Row : range.Start.Column,
            down ? range.RowCount : range.ColumnCount,
            insert,
            sheet.Name,
            down ? range.Start.Column : range.Start.Row,
            down ? range.End.Column : range.End.Row);
}
