using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class ValueCoercion
{

    private readonly ScalarReading _scalar = new();
    private readonly CellReading _cells = new();
    private readonly NumberReading _numbers = new();

    public CellValue ToScalar(EvalValue value, IEvaluationContext context) => value.Accept(_scalar, context);

    public CellValue Single(EvalValue value, IEvaluationContext context) => value switch
    {
        RangeValue { Range.IsSingleCell: false } range => _scalar.Intersect(range, context),
        ReferenceListValue { Areas: [{ Range.IsSingleCell: false } only] } => _scalar.Intersect(only, context),
        ReferenceListValue { Areas.Count: > 1 } => CellValue.FromError(CellError.Value),
        _ => ToScalar(value, context),
    };

    public IEnumerable<CellValue> Enumerate(EvalValue value, IEvaluationContext context) => value.Accept(_cells, context);

    public bool TryScalarNumber(EvalValue value, IEvaluationContext context, out double number, out CellError error)
    {
        error = CellError.None;
        var scalar = ToScalar(value, context);
        if (scalar.IsError) { number = 0; error = scalar.Error; return false; }
        if (!TryNumber(scalar, context, out number)) { error = CellError.Value; return false; }
        return true;
    }

    public bool TryNumber(CellValue value, IEvaluationContext context, out double number) =>
        value.Kind == ValueKind.Text
            ? context.TextNumbers.TryReadValue(value.AsText(), out number)
            : value.TryCoerceToNumber(out number);

    public bool TryCollectNumbers(
        IReadOnlyList<EvalValue> arguments, IEvaluationContext context, out List<double> numbers, out CellError error)
    {
        numbers = [];
        error = CellError.None;

        foreach (var argument in arguments)
            foreach (var reading in argument.Accept(_numbers, context))
            {
                if (reading.Error != CellError.None) { error = reading.Error; return false; }
                numbers.Add(reading.Number);
            }

        return true;
    }

    private sealed class ScalarReading : EvalValueVisitor<CellValue>
    {
        public override CellValue VisitScalar(ScalarValue value, IEvaluationContext context) => value.Value;

        public override CellValue VisitRange(RangeValue value, IEvaluationContext context)
        {
            if (value.Range.IsSingleCell) return context.GetCellValue(value.SheetName, value.Range.Start);
            if (context.ImplicitIntersection) return Intersection(value, context);
            context.NoteArrayUse();
            return context.GetRangeValues(value.SheetName, value.Range).FirstOrDefault(CellValue.Empty);
        }

        public CellValue Intersect(RangeValue value, IEvaluationContext context) => Intersection(value, context);

        private static CellValue Intersection(RangeValue value, IEvaluationContext context)
        {
            var range = value.Range;
            var here = context.CurrentCell.Address;
            var row = range.RowCount == 1 ? range.Start.Row : here.Row;
            var column = range.ColumnCount == 1 ? range.Start.Column : here.Column;
            return row >= range.Start.Row && row <= range.End.Row && column >= range.Start.Column && column <= range.End.Column
                ? context.GetCellValue(value.SheetName, new Addressing.CellAddress(row, column))
                : CellValue.FromError(CellError.Value);
        }

        public override CellValue VisitArray(ArrayValue value, IEvaluationContext context) =>
            value.Rows is [{ Count: > 0 } first, ..] ? first[0] : CellValue.Empty;

        public override CellValue VisitReferenceList(ReferenceListValue value, IEvaluationContext context) =>
            value.Areas is [var only] ? VisitRange(only, context) : CellValue.FromError(CellError.Value);

        public override CellValue VisitLambda(LambdaValue value, IEvaluationContext context) => CellValue.FromError(CellError.Calc);

        protected override CellValue Otherwise(EvalValue value, IEvaluationContext context) => CellValue.Empty;
    }

    private sealed class CellReading : EvalValueVisitor<IEnumerable<CellValue>>
    {
        public override IEnumerable<CellValue> VisitScalar(ScalarValue value, IEvaluationContext context) => [value.Value];

        public override IEnumerable<CellValue> VisitRange(RangeValue value, IEvaluationContext context) =>
            context.GetRangeValues(value.SheetName, value.Range);

        public override IEnumerable<CellValue> VisitArray(ArrayValue value, IEvaluationContext context) =>
            value.Rows.SelectMany(row => row);

        public override IEnumerable<CellValue> VisitReferenceList(ReferenceListValue value, IEvaluationContext context) =>
            value.Areas.SelectMany(area => VisitRange(area, context));

        protected override IEnumerable<CellValue> Otherwise(EvalValue value, IEvaluationContext context) => [];
    }

    private readonly record struct NumberRead(double Number, CellError Error);

    private sealed class NumberReading : EvalValueVisitor<IEnumerable<NumberRead>>
    {
        public override IEnumerable<NumberRead> VisitScalar(ScalarValue value, IEvaluationContext context)
        {
            var cell = value.Value;
            if (cell.IsError) return [new NumberRead(0, cell.Error)];
            if (cell.IsEmpty) return [];
            return cell.Kind == ValueKind.Text
                ? context.TextNumbers.TryReadValue(cell.AsText(), out var read) ? [new NumberRead(read, CellError.None)] : [new NumberRead(0, CellError.Value)]
                : cell.TryCoerceToNumber(out var number) ? [new NumberRead(number, CellError.None)] : [new NumberRead(0, CellError.Value)];
        }

        public override IEnumerable<NumberRead> VisitRange(RangeValue value, IEvaluationContext context) =>
            context.ContentWithin(value.SheetName, value.Range) is { } content
                ? Strict(context.GetRangeValues(value.SheetName, content))
                : [];

        public override IEnumerable<NumberRead> VisitArray(ArrayValue value, IEvaluationContext context) =>
            Strict(value.Rows.SelectMany(row => row));

        public override IEnumerable<NumberRead> VisitReferenceList(ReferenceListValue value, IEvaluationContext context) =>
            value.Areas.SelectMany(area => VisitRange(area, context));

        protected override IEnumerable<NumberRead> Otherwise(EvalValue value, IEvaluationContext context) => [];

        private IEnumerable<NumberRead> Strict(IEnumerable<CellValue> cells)
        {
            foreach (var cell in cells)
            {
                if (cell.IsError) { yield return new NumberRead(0, cell.Error); yield break; }
                if (cell.Kind == ValueKind.Number) yield return new NumberRead(cell.AsNumber(), CellError.None);
            }
        }
    }
}
