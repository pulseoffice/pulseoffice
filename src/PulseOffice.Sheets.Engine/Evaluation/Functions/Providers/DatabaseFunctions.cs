using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class DatabaseFunctions(IRangeMaterializer ranges, NumericText numbers) : IFunctionProvider
{
    public string Category => "Database";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        (string Name, Func<List<double>, CellValue> Reduce)[] aggregations =
        [
            ("DSUM", values => F.Number(values.Sum())),
            ("DAVERAGE", values => values.Count == 0 ? F.DivideByZero() : F.Number(values.Average())),
            ("DMAX", values => CellValue.Number(values.Count == 0 ? 0 : values.Max())),
            ("DMIN", values => CellValue.Number(values.Count == 0 ? 0 : values.Min())),
            ("DPRODUCT", values => F.Number(values.Count == 0 ? 0 : values.Aggregate(1.0, (a, b) => a * b))),
            ("DSTDEV", values => values.Count < 2 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.StandardDeviation(values, population: false))),
            ("DSTDEVP", values => values.Count < 1 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.StandardDeviation(values, population: true))),
            ("DVAR", values => values.Count < 2 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.Variance(values, population: false))),
            ("DVARP", values => values.Count < 1 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.Variance(values, population: true))),
        ];

        foreach (var (name, reduce) in aggregations)
            builder.Raw(name, 3, 3, r =>
            {
                var selection = Select(r);
                if (selection is null) return r.HasError ? r.Error : F.Invalid();

                var numbers = selection
                    .Where(v => v.Kind == ValueKind.Number)
                    .Select(v => v.AsNumber())
                    .ToList();
                return reduce(numbers);
            });

        builder.Raw("DCOUNT", 2, 3, r =>
        {
            if (!HasField(r)) return CountRecords(r);
            var selection = Select(r);
            if (selection is null) return r.HasError ? r.Error : F.Invalid();
            return CellValue.Number(selection.Count(v => v.Kind == ValueKind.Number));
        });

        builder.Raw("DCOUNTA", 2, 3, r =>
        {
            if (!HasField(r)) return CountRecords(r);
            var selection = Select(r);
            if (selection is null) return r.HasError ? r.Error : F.Invalid();
            return CellValue.Number(selection.Count(v => !v.IsEmpty));
        });

        builder.Raw("DGET", 3, 3, r =>
        {
            var selection = Select(r);
            if (selection is null) return r.HasError ? r.Error : F.Invalid();

            return selection.Count switch
            {
                0 => CellValue.FromError(CellError.Value),
                1 => selection[0],
                _ => CellValue.FromError(CellError.Number),
            };
        });

        return builder.Build();
    }

    private static bool HasField(ArgumentReader r) => r.Count == 3 && r.Has(1);

    private CellValue CountRecords(ArgumentReader r)
    {
        var database = ranges.Materialize(r.Raw(0), r.Context);
        var criteria = ranges.Materialize(r.Raw(r.Count - 1), r.Context);
        if (r.HasError) return r.Error;
        if (database.RowCount < 2 || criteria.RowCount < 1) return F.Invalid();
        var filter = Compile(database, criteria, r.Raw(r.Count - 1), r.Context);
        return CellValue.Number(Enumerable.Range(1, database.RowCount - 1).Count(row => filter.Matches(database, row)));
    }

    private List<CellValue>? Select(ArgumentReader r)
    {
        var database = ranges.Materialize(r.Raw(0), r.Context);
        var criteria = ranges.Materialize(r.Raw(2), r.Context);
        if (r.HasError || database.RowCount < 2 || criteria.RowCount < 1) return null;

        var fieldIndex = ResolveField(r.Value(1), database);
        if (fieldIndex < 0) return null;

        var filter = Compile(database, criteria, r.Raw(2), r.Context);
        var matches = new List<CellValue>();

        for (var row = 1; row < database.RowCount; row++)
            if (filter.Matches(database, row))
                matches.Add(database[row, fieldIndex]);

        return matches;
    }

    private static int ResolveField(CellValue field, ValueGrid database)
    {
        if (field.Kind == ValueKind.Number)
        {
            var index = (int)field.AsNumber();
            return index >= 1 && index <= database.ColumnCount ? index - 1 : -1;
        }

        return FindColumn(database, field.ToString());
    }

    private RecordFilter Compile(ValueGrid database, ValueGrid criteria, EvalValue criteriaSource, IEvaluationContext context)
    {
        var alternatives = new List<IReadOnlyList<IRecordCondition>>();

        for (var criteriaRow = 1; criteriaRow < criteria.RowCount; criteriaRow++)
        {
            var conditions = new List<IRecordCondition>();
            var possible = true;

            for (var criteriaColumn = 0; criteriaColumn < criteria.ColumnCount && possible; criteriaColumn++)
            {
                var condition = criteria[criteriaRow, criteriaColumn];
                if (condition.IsEmpty || (condition.Kind == ValueKind.Text && condition.AsText().Length == 0)) continue;

                var column = FindColumn(database, criteria[0, criteriaColumn].ToString());
                if (column >= 0)
                    conditions.Add(new FieldCondition(column, CriteriaMatcher.Parse(condition, numbers, context.TextRules)));
                else if (Calculated(criteriaSource, criteriaRow, criteriaColumn, context) is { } calculated)
                    conditions.Add(calculated);
                else
                    possible = false;
            }

            if (possible) alternatives.Add(conditions);
        }

        return new RecordFilter(alternatives, criteria.RowCount <= 1);
    }

    private static CalculatedCondition? Calculated(EvalValue criteriaSource, int row, int column, IEvaluationContext context)
    {
        if (criteriaSource is not RangeValue range) return null;
        var address = new CellAddress(range.Range.Start.Row + row, range.Range.Start.Column + column);
        return context.GetFormulaText(range.SheetName, address) is null
            ? null
            : new CalculatedCondition(context, range.SheetName, address);
    }

    private static int FindColumn(ValueGrid database, string header)
    {
        for (var column = 0; column < database.ColumnCount; column++)
            if (string.Equals(database[0, column].ToString(), header, StringComparison.OrdinalIgnoreCase))
                return column;
        return -1;
    }

    private interface IRecordCondition
    {
        bool Matches(ValueGrid database, int row);
    }

    private sealed class FieldCondition(int column, CriteriaMatcher matcher) : IRecordCondition
    {
        public bool Matches(ValueGrid database, int row) => matcher.Matches(database[row, column]);
    }

    private sealed class CalculatedCondition(IEvaluationContext context, string? sheetName, CellAddress formula) : IRecordCondition
    {
        public bool Matches(ValueGrid database, int row) => context.EvaluateOffset(sheetName, formula, row - 1, 0) switch
        {
            { Kind: ValueKind.Boolean } flag => flag.AsBoolean(),
            { Kind: ValueKind.Number } number => number.AsNumber() != 0,
            _ => false,
        };
    }

    private sealed class RecordFilter(IReadOnlyList<IReadOnlyList<IRecordCondition>> alternatives, bool matchesEverything)
    {
        public bool Matches(ValueGrid database, int row) =>
            matchesEverything || alternatives.Any(conditions => conditions.All(condition => condition.Matches(database, row)));
    }
}
