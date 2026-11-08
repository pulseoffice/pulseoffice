using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record MultipleOperationsRequest
{
    public required CellRange Target { get; init; }

    public required IReadOnlyList<SheetCell> Formulas { get; init; }

    public SheetCell? RowInput { get; init; }

    public SheetCell? ColumnInput { get; init; }

    public bool IsValid => Formulas.Count > 0 && (RowInput is not null || ColumnInput is not null);
}

public sealed record MultipleOperationsResult(IReadOnlyDictionary<CellAddress, CellValue> Values);

public interface IMultipleOperations
{
    MultipleOperationsResult Compute(CalculationEngine engine, int sheetIndex, MultipleOperationsRequest request);
}

public sealed class MultipleOperations : IMultipleOperations
{
    public MultipleOperationsResult Compute(
        CalculationEngine engine, int sheetIndex, MultipleOperationsRequest request)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(request);

        var values = new Dictionary<CellAddress, CellValue>();
        if (!request.IsValid) return new MultipleOperationsResult(values);

        var sheet = engine.Workbook.Sheets[sheetIndex];
        var target = request.Target;

        var rowInputOriginal = Save(engine, request.RowInput);
        var columnInputOriginal = Save(engine, request.ColumnInput);

        try
        {
            if (request.RowInput is not null && request.ColumnInput is not null)
                Cross(engine, sheet, request, values);
            else
                Single(engine, sheet, request, values);
        }
        finally
        {
            Restore(engine, request.RowInput, rowInputOriginal);
            Restore(engine, request.ColumnInput, columnInputOriginal);
        }

        return new MultipleOperationsResult(values);
    }

    private void Single(
        CalculationEngine engine, Worksheet sheet,
        MultipleOperationsRequest request, Dictionary<CellAddress, CellValue> values)
    {
        var target = request.Target;
        var byColumn = request.ColumnInput is not null;
        var input = request.ColumnInput ?? request.RowInput!;

        if (byColumn)
        {
            for (var row = target.Start.Row; row <= target.End.Row; row++)
            {
                var source = sheet.GetValue(new CellAddress(row, target.Start.Column));
                if (source.IsEmpty) continue;

                engine.SetValue(input.Value, source);

                for (var index = 0; index < request.Formulas.Count; index++)
                {
                    var column = target.Start.Column + 1 + index;
                    if (column > target.End.Column) break;

                    values[new CellAddress(row, column)] = engine.GetValue(request.Formulas[index]);
                }
            }

            return;
        }

        for (var column = target.Start.Column; column <= target.End.Column; column++)
        {
            var source = sheet.GetValue(new CellAddress(target.Start.Row, column));
            if (source.IsEmpty) continue;

            engine.SetValue(input.Value, source);

            for (var index = 0; index < request.Formulas.Count; index++)
            {
                var row = target.Start.Row + 1 + index;
                if (row > target.End.Row) break;

                values[new CellAddress(row, column)] = engine.GetValue(request.Formulas[index]);
            }
        }
    }

    private void Cross(
        CalculationEngine engine, Worksheet sheet,
        MultipleOperationsRequest request, Dictionary<CellAddress, CellValue> values)
    {
        var target = request.Target;
        var formula = request.Formulas[0];

        for (var row = target.Start.Row + 1; row <= target.End.Row; row++)
        {
            var rowValue = sheet.GetValue(new CellAddress(row, target.Start.Column));
            if (rowValue.IsEmpty) continue;

            engine.SetValue(request.ColumnInput!.Value, rowValue);

            for (var column = target.Start.Column + 1; column <= target.End.Column; column++)
            {
                var columnValue = sheet.GetValue(new CellAddress(target.Start.Row, column));
                if (columnValue.IsEmpty) continue;

                engine.SetValue(request.RowInput!.Value, columnValue);
                values[new CellAddress(row, column)] = engine.GetValue(formula);
            }
        }
    }

    private Cell? Save(CalculationEngine engine, SheetCell? cell) =>
        cell is null ? null : engine.Workbook.Sheets[cell.Value.SheetIndex][cell.Value.Address];

    private void Restore(CalculationEngine engine, SheetCell? cell, Cell? original)
    {
        if (cell is null || original is null) return;

        if (original.HasFormula) engine.SetFormula(cell.Value, original.Formula!);
        else engine.SetValue(cell.Value, original.Value);
    }
}
