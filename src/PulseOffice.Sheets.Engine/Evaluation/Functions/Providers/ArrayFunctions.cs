using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ArrayFunctions(IRangeMaterializer ranges, IRandomSource random) : IFunctionProvider
{
    public string Category => "Matrice";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterShaping(builder);
        RegisterSelection(builder);
        RegisterGeneration(builder);
        RegisterMatrixAlgebra(builder);
        RegisterStatisticalArrays(builder);

        return builder.Build();
    }

    private void RegisterShaping(FunctionBuilder builder)
    {
        builder.ArrayResult("TRANSPOSE", 1, 1, r =>
        {
            var grid = Grid(r, 0);
            var rows = new List<IReadOnlyList<CellValue>>(grid.ColumnCount);

            for (var column = 0; column < grid.ColumnCount; column++)
                rows.Add([.. grid.Column(column)]);

            return new ArrayValue(rows);
        });

        builder.ArrayResult("HSTACK", 1, int.MaxValue, r => Stack(r, horizontal: true));
        builder.ArrayResult("VSTACK", 1, int.MaxValue, r => Stack(r, horizontal: false));

        builder.ArrayResult("TOCOL", 1, 3, r => Flatten(r, toColumn: true));
        builder.ArrayResult("TOROW", 1, 3, r => Flatten(r, toColumn: false));

        builder.ArrayResult("WRAPROWS", 2, 3, r => Wrap(r, byRow: true));
        builder.ArrayResult("WRAPCOLS", 2, 3, r => Wrap(r, byRow: false));

        builder.ArrayResult("EXPAND", 2, 4, r =>
        {
            var grid = Grid(r, 0);
            var rows = r.Integer(1, grid.RowCount);
            var columns = r.Integer(2, grid.ColumnCount);
            var padding = r.Has(3) ? r.Value(3) : CellValue.FromError(CellError.NotAvailable);
            if (r.HasError) return r.Error.AsEval();
            if (grid.RowCount < 1 || grid.ColumnCount < 1 || rows < grid.RowCount || columns < grid.ColumnCount) return EvalValue.Error(CellError.Number);
            if (!Allocatable(rows, columns)) return EvalValue.Error(CellError.Number);

            var result = new List<IReadOnlyList<CellValue>>(rows);
            for (var row = 0; row < rows; row++)
            {
                var line = new List<CellValue>(columns);
                for (var column = 0; column < columns; column++)
                    line.Add(row < grid.RowCount && column < grid.ColumnCount
                        ? grid[row, column] : padding);
                result.Add(line);
            }
            return new ArrayValue(result);
        });
    }

    private void RegisterSelection(FunctionBuilder builder)
    {
        builder.ArrayResult("TAKE", 1, 3, r => Slice(r, fromStart: true));
        builder.ArrayResult("DROP", 1, 3, r => Slice(r, fromStart: false));

        builder.ArrayResult("CHOOSEROWS", 2, int.MaxValue, r => Choose(r, byRow: true));
        builder.ArrayResult("CHOOSECOLS", 2, int.MaxValue, r => Choose(r, byRow: false));

        builder.ArrayResult("FILTER", 2, 3, r =>
        {
            var grid = Grid(r, 0);
            var mask = Grid(r, 1);
            if (r.HasError) return r.Error.AsEval();

            var byRow = mask.ColumnCount == 1;
            var flags = (byRow ? mask.Column(0) : mask.Row(0)).ToList();

            var kept = new List<IReadOnlyList<CellValue>>();
            if (byRow)
            {
                for (var row = 0; row < grid.RowCount && row < flags.Count; row++)
                    if (IsTrue(flags[row])) kept.Add([.. grid.Row(row)]);
            }
            else
            {
                var columns = new List<int>();
                for (var column = 0; column < grid.ColumnCount && column < flags.Count; column++)
                    if (IsTrue(flags[column])) columns.Add(column);

                for (var row = 0; row < grid.RowCount; row++)
                    kept.Add([.. columns.Select(c => grid[row, c])]);
            }

            if (kept.Count == 0 || (kept.Count > 0 && kept[0].Count == 0))
                return r.Has(2)
                    ? EvalValue.Scalar(r.Value(2))
                    : EvalValue.Error(CellError.Calc);

            return new ArrayValue(kept);
        });

        builder.ArrayResult("SORT", 1, 4, r =>
        {
            var grid = Grid(r, 0);
            var index = r.Integer(1, 1);
            var order = r.Integer(2, 1);
            var byColumn = r.Boolean(3, false);
            if (r.HasError) return r.Error.AsEval();
            if (order is not (1 or -1)) return EvalValue.Error(CellError.Value);

            return Sort(grid, index - 1, order == 1, byColumn);
        });

        builder.ArrayResult("SORTBY", 2, int.MaxValue, r =>
        {
            var grid = Grid(r, 0);
            if (r.HasError) return r.Error.AsEval();

            var keys = new List<(IReadOnlyList<CellValue> Values, int Direction)>();
            bool? byColumn = null;
            for (var i = 1; i < r.Count; i += 2)
            {
                var key = Grid(r, i);
                var order = r.Integer(i + 1, 1);
                if (r.HasError) return r.Error.AsEval();
                if (order is not (1 or -1)) return EvalValue.Error(CellError.Value);

                var columnKey = key.ColumnCount == 1 && key.RowCount == grid.RowCount;
                var rowKey = key.RowCount == 1 && key.ColumnCount == grid.ColumnCount && !columnKey;
                if (!columnKey && !rowKey) return EvalValue.Error(CellError.Value);
                if (byColumn is { } existing && existing != rowKey) return EvalValue.Error(CellError.Value);
                byColumn = rowKey;
                keys.Add(([.. rowKey ? key.Row(0) : key.Column(0)], order));
            }

            var count = byColumn == true ? grid.ColumnCount : grid.RowCount;
            IOrderedEnumerable<int>? sorted = null;
            foreach (var (values, direction) in keys)
            {
                var comparer = Comparer<int>.Create((x, y) => Compare(values[x], values[y]) * direction);
                sorted = sorted is null
                    ? Enumerable.Range(0, count).Order(comparer)
                    : sorted.ThenBy(i => i, comparer);
            }
            var indices = sorted!.ToList();

            if (byColumn == true)
                return new ArrayValue([.. Enumerable.Range(0, grid.RowCount)
                    .Select(row => (IReadOnlyList<CellValue>)[.. indices.Select(column => grid[row, column])])]);
            return new ArrayValue([.. indices.Select(i => (IReadOnlyList<CellValue>)[.. grid.Row(i)])]);
        });

        builder.ArrayResult("UNIQUE", 1, 3, r =>
        {
            var grid = Grid(r, 0);
            var byColumn = r.Boolean(1, false);
            var onlyOnce = r.Boolean(2, false);
            if (r.HasError) return r.Error.AsEval();

            var lines = byColumn
                ? Enumerable.Range(0, grid.ColumnCount).Select(c => (IReadOnlyList<CellValue>)[.. grid.Column(c)]).ToList()
                : Enumerable.Range(0, grid.RowCount).Select(x => (IReadOnlyList<CellValue>)[.. grid.Row(x)]).ToList();

            var counts = new Dictionary<IReadOnlyList<CellValue>, int>(_lineEquality);
            var firstSeen = new List<IReadOnlyList<CellValue>>();
            foreach (var line in lines)
            {
                if (counts.TryGetValue(line, out var seen)) counts[line] = seen + 1;
                else
                {
                    counts[line] = 1;
                    firstSeen.Add(line);
                }
            }

            var kept = firstSeen.Where(line => !onlyOnce || counts[line] == 1).ToList();
            if (kept.Count == 0) return EvalValue.Error(CellError.Calc);

            return byColumn ? Transpose(kept) : new ArrayValue(kept);
        });
    }

    private void RegisterGeneration(FunctionBuilder builder)
    {
        builder.ArrayResult("SEQUENCE", 1, 4, r =>
        {
            var rows = r.Integer(0, 1);
            var columns = r.Integer(1, 1);
            var start = r.Number(2, 1);
            var step = r.Number(3, 1);
            if (r.HasError) return r.Error.AsEval();
            if (rows < 0 || columns < 0) return EvalValue.Error(CellError.Value);
            if (rows == 0 || columns == 0) return EvalValue.Error(CellError.Calc);
            if (!Allocatable(rows, columns)) return EvalValue.Error(CellError.Number);

            var result = new List<IReadOnlyList<CellValue>>(rows);
            var current = start;

            for (var row = 0; row < rows; row++)
            {
                var line = new List<CellValue>(columns);
                for (var column = 0; column < columns; column++)
                {
                    line.Add(CellValue.Number(current));
                    current += step;
                }
                result.Add(line);
            }
            return new ArrayValue(result);
        });

        builder.ArrayResult("RANDARRAY", 0, 5, r =>
        {
            var rows = r.Integer(0, 1);
            var columns = r.Integer(1, 1);
            var wholeNumbers = r.Boolean(4, false);
            var minimum = wholeNumbers ? M.Ceiling(r.Number(2, 0)) : r.Number(2, 0);
            var maximum = wholeNumbers ? M.Ceiling(r.Number(3, 1)) : r.Number(3, 1);
            if (r.HasError) return r.Error.AsEval();
            if (rows < 0 || columns < 0 || minimum > maximum) return EvalValue.Error(CellError.Value);
            if (rows == 0 || columns == 0) return EvalValue.Error(CellError.Calc);
            if (!Allocatable(rows, columns)) return EvalValue.Error(CellError.Number);

            var result = new List<IReadOnlyList<CellValue>>(rows);
            for (var row = 0; row < rows; row++)
            {
                var line = new List<CellValue>(columns);
                for (var column = 0; column < columns; column++)
                    line.Add(CellValue.Number(wholeNumbers
                        ? random.NextInt64((long)minimum, (long)maximum)
                        : minimum + random.NextDouble() * (maximum - minimum)));
                result.Add(line);
            }
            return new ArrayValue(result);
        });

        builder.ArrayResult("TEXTSPLIT", 2, 6, r =>
        {
            var text = r.Text(0);
            var columnDelimiters = Delimiters(r, 1);
            var rowDelimiters = Delimiters(r, 2);
            var ignoreEmpty = r.Boolean(3, false);
            var matchMode = r.Integer(4, 0);
            var padding = r.Has(5) ? r.Value(5) : CellValue.FromError(CellError.NotAvailable);
            if (r.HasError) return r.Error.AsEval();
            if (matchMode is not (0 or 1)) return EvalValue.Error(CellError.Value);
            if (columnDelimiters.Count == 0 && rowDelimiters.Count == 0) return EvalValue.Error(CellError.Value);

            var comparison = matchMode == 1 ? StringComparison.CurrentCultureIgnoreCase : StringComparison.Ordinal;
            var lines = _splitter.Split(text, rowDelimiters, ignoreEmpty, comparison)
                .Select(line => _splitter.Split(line, columnDelimiters, ignoreEmpty, comparison))
                .Where(parts => parts.Count > 0)
                .ToList();
            if (lines.Count == 0) return EvalValue.Error(CellError.Value);

            var width = lines.Max(parts => parts.Count);
            var result = lines
                .Select(parts => (IReadOnlyList<CellValue>)
                    [.. Enumerable.Range(0, width).Select(column => column < parts.Count ? CellValue.Text(parts[column]) : padding)])
                .ToList();

            return new ArrayValue(result);
        });
    }

    private void RegisterMatrixAlgebra(FunctionBuilder builder)
    {
        builder.ArrayResult("MUNIT", 1, 1, r =>
        {
            var size = r.Integer(0);
            if (r.HasError) return r.Error.AsEval();
            if (size < 1) return EvalValue.Error(CellError.Value);
            if (!Allocatable(size, size)) return EvalValue.Error(CellError.Number);

            var result = new List<IReadOnlyList<CellValue>>(size);
            for (var row = 0; row < size; row++)
                result.Add([.. Enumerable.Range(0, size).Select(c => CellValue.Number(c == row ? 1 : 0))]);

            return new ArrayValue(result);
        });

        builder.ArrayResult("MMULT", 2, 2, r =>
        {
            var left = Numbers(r, 0);
            var right = Numbers(r, 1);
            if (r.HasError) return r.Error.AsEval();
            if (left is null || right is null) return EvalValue.Error(CellError.Value);

            var inner = left.GetLength(1);

            if (inner != right.GetLength(0)) return EvalValue.Error(CellError.Number);

            var rows = left.GetLength(0);
            var columns = right.GetLength(1);
            if (!Allocatable(rows, columns)) return EvalValue.Error(CellError.Number);
            var result = new List<IReadOnlyList<CellValue>>(rows);

            for (var row = 0; row < rows; row++)
            {
                var line = new List<CellValue>(columns);
                for (var column = 0; column < columns; column++)
                {
                    var total = 0.0;
                    for (var k = 0; k < inner; k++) total += left[row, k] * right[k, column];
                    line.Add(CellValue.Number(total));
                }
                result.Add(line);
            }
            return new ArrayValue(result);
        });

        builder.Raw("MDETERM", 1, 1, r =>
        {
            var matrix = Numbers(r, 0);
            if (r.HasError) return r.Error;
            if (matrix is null) return F.Invalid();
            if (matrix.GetLength(0) != matrix.GetLength(1) || matrix.GetLength(0) == 0) return F.Invalid();

            return F.Number(MatrixAlgebra.Determinant(matrix));
        });

        builder.ArrayResult("MINVERSE", 1, 1, r =>
        {
            var matrix = Numbers(r, 0);
            if (r.HasError) return r.Error.AsEval();
            if (matrix is null) return EvalValue.Error(CellError.Value);
            if (matrix.GetLength(0) != matrix.GetLength(1)) return EvalValue.Error(CellError.Value);

            var inverse = MatrixAlgebra.Invert(matrix);
            if (inverse is null) return EvalValue.Error(CellError.Number);

            var size = inverse.GetLength(0);
            var result = new List<IReadOnlyList<CellValue>>(size);
            for (var row = 0; row < size; row++)
                result.Add([.. Enumerable.Range(0, size).Select(c => CellValue.Number(inverse[row, c]))]);

            return new ArrayValue(result);
        });
    }

    private void RegisterStatisticalArrays(FunctionBuilder builder)
    {
        builder.ArrayResult("FREQUENCY", 2, 2, r =>
        {
            var data = Series(r, 0);
            var bins = Series(r, 1);
            if (r.HasError) return r.Error.AsEval();

            var order = Enumerable.Range(0, bins.Count).OrderBy(i => bins[i]).ThenBy(i => i).ToList();
            var bounds = order.Select(i => bins[i]).ToArray();
            var tally = new double[bins.Count + 1];
            foreach (var value in data)
            {
                var position = Array.BinarySearch(bounds, value);
                if (position < 0) position = ~position;
                else while (position > 0 && bounds[position - 1] == value) position--;
                tally[position == bounds.Length ? bins.Count : order[position]]++;
            }

            var counts = new List<IReadOnlyList<CellValue>>(bins.Count + 1);
            counts.AddRange(tally.Select(count => (IReadOnlyList<CellValue>)[CellValue.Number(count)]));

            return new ArrayValue(counts);
        });

        builder.ArrayResult("MODE.MULT", 1, int.MaxValue, r =>
        {
            if (!r.TryAllNumbers(out var numbers)) return r.Error.AsEval();

            var groups = numbers.GroupBy(n => n)
                                .Select(g => (Value: g.Key, Count: g.Count()))
                                .OrderByDescending(g => g.Count)
                                .ToList();

            if (groups.Count == 0 || groups[0].Count < 2) return EvalValue.Error(CellError.NotAvailable);

            var best = groups[0].Count;
            return new ArrayValue([.. groups.Where(g => g.Count == best)
                .Select(g => (IReadOnlyList<CellValue>)[CellValue.Number(g.Value)])]);
        });

        foreach (var (name, exponential) in ((string, bool)[])[("TREND", false), ("GROWTH", true)])
            builder.ArrayResult(name, 1, 4, r => Projection(r, exponential));

        foreach (var (name, exponential) in ((string, bool)[])[("LINEST", false), ("LOGEST", true)])
            builder.ArrayResult(name, 1, 4, r => Regression(r, exponential));
    }

    private sealed record RegressionInput(List<double> Y, List<double[]> X, int Regressors, bool ByRows, bool SameShape);

    private EvalValue? ReadRegression(ArgumentReader r, bool exponential, out RegressionInput input)
    {
        input = new RegressionInput([], [], 1, true, true);
        var known = Grid(r, 0);
        if (r.HasError) return r.Error.AsEval();

        var y = new List<double>();
        foreach (var value in known.InReadingOrder())
        {
            if (value.IsError) return EvalValue.Error(value.Error);
            if (value.Kind != ValueKind.Number) return EvalValue.Error(CellError.Value);
            if (exponential && value.AsNumber() <= 0) return EvalValue.Error(CellError.Number);
            y.Add(exponential ? M.Log(value.AsNumber()) : value.AsNumber());
        }

        var byRows = known.ColumnCount == 1;
        var x = new List<double[]>();
        if (!r.Has(1))
        {
            x.AddRange(Enumerable.Range(1, y.Count).Select(i => new[] { (double)i }));
            input = new RegressionInput(y, x, 1, byRows, true);
            return null;
        }

        var given = Grid(r, 1);
        var sameShape = given.RowCount == known.RowCount && given.ColumnCount == known.ColumnCount;
        var count = sameShape ? 1 : byRows ? given.ColumnCount : given.RowCount;
        if (!sameShape && (byRows ? given.RowCount : given.ColumnCount) != y.Count) return EvalValue.Error(CellError.Reference);
        var flat = sameShape ? given.InReadingOrder().ToList() : null;
        for (var i = 0; i < y.Count; i++)
        {
            var row = new double[count];
            for (var j = 0; j < count; j++)
            {
                var value = flat is not null ? flat[i] : byRows ? given[i, j] : given[j, i];
                if (value.IsError) return EvalValue.Error(value.Error);
                if (value.Kind != ValueKind.Number) return EvalValue.Error(CellError.Value);
                row[j] = value.AsNumber();
            }
            x.Add(row);
        }
        input = new RegressionInput(y, x, count, byRows, sameShape);
        return null;
    }

    private EvalValue Projection(ArgumentReader r, bool exponential)
    {
        if (ReadRegression(r, exponential, out var input) is { } failure) return failure;
        var withIntercept = r.Boolean(3, true);
        if (r.HasError) return r.Error.AsEval();
        if (LeastSquares.Fit(input.Y, input.X, withIntercept) is not { } fit) return EvalValue.Error(CellError.Number);

        double Predict(double[] point)
        {
            var value = fit.Intercept;
            for (var j = 0; j < point.Length; j++) value += fit.Coefficients[j] * point[j];
            return exponential ? M.Exp(value) : value;
        }

        if (!r.Has(2))
            return input.ByRows
                ? new ArrayValue([.. input.X.Select(point => (IReadOnlyList<CellValue>)[F.Number(Predict(point))])])
                : new ArrayValue([[.. input.X.Select(point => F.Number(Predict(point)))]]);

        var targets = Grid(r, 2);
        if (input.Regressors == 1)
            return new ArrayValue([.. Enumerable.Range(0, targets.RowCount).Select(row => (IReadOnlyList<CellValue>)[..
                Enumerable.Range(0, targets.ColumnCount).Select(column => targets[row, column] is { Kind: ValueKind.Number } value
                    ? F.Number(Predict([value.AsNumber()])) : CellValue.FromError(CellError.Value))])]);

        var points = input.ByRows ? targets.RowCount : targets.ColumnCount;
        if ((input.ByRows ? targets.ColumnCount : targets.RowCount) != input.Regressors) return EvalValue.Error(CellError.Reference);
        var predictions = Enumerable.Range(0, points).Select(index =>
        {
            var point = new double[input.Regressors];
            for (var j = 0; j < input.Regressors; j++)
            {
                var value = input.ByRows ? targets[index, j] : targets[j, index];
                if (value.Kind != ValueKind.Number) return CellValue.FromError(CellError.Value);
                point[j] = value.AsNumber();
            }
            return F.Number(Predict(point));
        }).ToList();
        return input.ByRows
            ? new ArrayValue([.. predictions.Select(value => (IReadOnlyList<CellValue>)[value])])
            : new ArrayValue([predictions]);
    }

    private EvalValue Regression(ArgumentReader r, bool exponential)
    {
        if (ReadRegression(r, exponential, out var input) is { } failure) return failure;
        var withIntercept = r.Boolean(2, true);
        var withStatistics = r.Boolean(3, false);
        if (r.HasError) return r.Error.AsEval();

        if (LeastSquares.Fit(input.Y, input.X, withIntercept) is not { } fit) return EvalValue.Error(CellError.Number);
        var columns = fit.Coefficients.Length + 1;
        CellValue Shown(double value) => double.IsFinite(value) ? CellValue.Number(value) : CellValue.FromError(CellError.NotAvailable);
        CellValue Coefficient(double value) => CellValue.Number(exponential ? M.Exp(value) : value);

        var rows = new List<IReadOnlyList<CellValue>>();
        rows.Add([.. fit.Coefficients.Reverse().Select(Coefficient), Coefficient(fit.Intercept)]);
        if (withStatistics)
        {
            var missing = CellValue.FromError(CellError.NotAvailable);
            List<CellValue> Padded(params CellValue[] values) => [.. values, .. Enumerable.Repeat(missing, columns - values.Length)];
            rows.Add([.. fit.CoefficientErrors.Reverse().Select(Shown), withIntercept ? Shown(fit.InterceptError) : missing]);
            rows.Add(Padded(Shown(fit.RSquared), Shown(fit.StandardError)));
            rows.Add(Padded(Shown(fit.FStatistic), CellValue.Number(fit.DegreesOfFreedom)));
            rows.Add(Padded(CellValue.Number(fit.RegressionSumOfSquares), CellValue.Number(fit.ResidualSumOfSquares)));
        }
        return new ArrayValue(rows);
    }

    private ValueGrid Grid(ArgumentReader r, int index) => ranges.Materialize(r.Raw(index), r.Context);

    private List<double> Series(ArgumentReader r, int index)
    {
        var values = new List<double>();
        foreach (var value in Grid(r, index).InReadingOrder())
        {
            if (value.IsError) { r.Fail(value.Error); return values; }
            if (value.Kind == ValueKind.Number) values.Add(value.AsNumber());
        }
        return values;
    }

    private double[,]? Numbers(ArgumentReader r, int index)
    {
        var grid = Grid(r, index);
        if (grid.IsEmpty) return null;

        var matrix = new double[grid.RowCount, grid.ColumnCount];
        for (var row = 0; row < grid.RowCount; row++)
            for (var column = 0; column < grid.ColumnCount; column++)
            {
                var value = grid[row, column];
                if (value.IsError) { r.Fail(value.Error); return null; }
                if (!value.TryCoerceToNumber(out var number)) return null;
                matrix[row, column] = number;
            }
        return matrix;
    }

    private EvalValue Stack(ArgumentReader r, bool horizontal)
    {
        var grids = new List<ValueGrid>();
        for (var i = 0; i < r.Count; i++) grids.Add(Grid(r, i));
        if (r.HasError) return r.Error.AsEval();

        if (horizontal)
        {
            var rows = grids.Max(g => g.RowCount);
            var result = new List<IReadOnlyList<CellValue>>(rows);

            for (var row = 0; row < rows; row++)
            {
                var line = new List<CellValue>();
                foreach (var grid in grids)
                    for (var column = 0; column < grid.ColumnCount; column++)

                        line.Add(row < grid.RowCount ? grid[row, column] : CellValue.FromError(CellError.NotAvailable));
                result.Add(line);
            }
            return new ArrayValue(result);
        }

        var columns = grids.Max(g => g.ColumnCount);
        var stacked = new List<IReadOnlyList<CellValue>>();

        foreach (var grid in grids)
            for (var row = 0; row < grid.RowCount; row++)
            {
                var line = new List<CellValue>(columns);
                for (var column = 0; column < columns; column++)
                    line.Add(column < grid.ColumnCount ? grid[row, column] : CellValue.FromError(CellError.NotAvailable));
                stacked.Add(line);
            }

        return new ArrayValue(stacked);
    }

    private EvalValue Flatten(ArgumentReader r, bool toColumn)
    {
        var grid = Grid(r, 0);
        var ignore = r.Integer(1, 0);
        var byColumn = r.Boolean(2, false);
        if (r.HasError) return r.Error.AsEval();

        var values = byColumn
            ? Enumerable.Range(0, grid.ColumnCount).SelectMany(c => grid.Column(c))
            : grid.InReadingOrder();

        var kept = values.Where(v =>
            !((ignore is 1 or 3 && v.IsEmpty) || (ignore is 2 or 3 && v.IsError))).ToList();

        if (kept.Count == 0) return EvalValue.Error(CellError.Calc);

        return toColumn
            ? new ArrayValue([.. kept.Select(v => (IReadOnlyList<CellValue>)[v])])
            : new ArrayValue([kept]);
    }

    private EvalValue Wrap(ArgumentReader r, bool byRow)
    {
        var grid = Grid(r, 0);
        var width = r.Integer(1);
        var padding = r.Has(2) ? r.Value(2) : CellValue.FromError(CellError.NotAvailable);
        if (r.HasError) return r.Error.AsEval();
        if (width < 1) return EvalValue.Error(CellError.Value);

        var values = grid.InReadingOrder().ToList();
        if (!Allocatable((values.Count + (long)width - 1) / width, width)) return EvalValue.Error(CellError.Number);
        var lines = new List<IReadOnlyList<CellValue>>();

        for (var start = 0; start < values.Count; start += width)
        {
            var line = new List<CellValue>(width);
            for (var offset = 0; offset < width; offset++)
                line.Add(start + offset < values.Count ? values[start + offset] : padding);
            lines.Add(line);
        }

        return byRow ? new ArrayValue(lines) : Transpose(lines);
    }

    private EvalValue Slice(ArgumentReader r, bool fromStart)
    {
        var rowCount = r.Has(1) ? r.Integer(1) : (int?)null;
        var columnCount = r.Has(2) ? r.Integer(2) : (int?)null;
        if (r.HasError) return r.Error.AsEval();
        if (rowCount is null && columnCount is null) return EvalValue.Error(CellError.Number);

        var grid = Grid(r, 0);
        if (r.HasError) return r.Error.AsEval();
        if (grid.RowCount < 1 || grid.ColumnCount < 1) return EvalValue.Error(CellError.Number);

        if (SliceRange(grid.RowCount, rowCount, fromStart) is not var (firstRow, endRow)
            || SliceRange(grid.ColumnCount, columnCount, fromStart) is not var (firstColumn, endColumn))
            return EvalValue.Error(CellError.Calc);
        if (endRow <= firstRow || endColumn <= firstColumn) return EvalValue.Error(CellError.Calc);

        return new ArrayValue([.. Enumerable.Range(firstRow, endRow - firstRow).Select(row =>
            (IReadOnlyList<CellValue>)[.. Enumerable.Range(firstColumn, endColumn - firstColumn).Select(column => grid[row, column])])]);
    }

    private (int First, int End)? SliceRange(int total, int? count, bool take)
    {
        if (count is not { } value) return (0, total);
        if (M.Abs(value) < total)
            return (take, value < 0) switch
            {
                (true, true) => (total + value, total),
                (true, false) => (0, value),
                (false, true) => (0, total + value),
                _ => (value, total),
            };
        return take ? (0, total) : null;
    }

    private EvalValue Choose(ArgumentReader r, bool byRow)
    {
        var grid = Grid(r, 0);
        if (r.HasError) return r.Error.AsEval();
        if (grid.RowCount < 1 || grid.ColumnCount < 1) return EvalValue.Error(CellError.Number);
        var indices = new List<int>();
        var total = byRow ? grid.RowCount : grid.ColumnCount;

        for (var i = 1; i < r.Count; i++)
        {
            if (!r.Has(i)) return EvalValue.Error(CellError.Value);
            foreach (var value in r.Values(i))
            {
                if (value.Kind is not ValueKind.Number and not ValueKind.Boolean) return EvalValue.Error(value.IsError ? value.Error : CellError.Value);
                var index = (int)M.Truncate(value.AsNumber());
                if (index < 0) index = total + index + 1;
                if (index < 1 || index > total) return EvalValue.Error(CellError.Value);
                indices.Add(index - 1);
            }
        }
        if (r.HasError) return r.Error.AsEval();

        return byRow
            ? new ArrayValue([.. indices.Select(row => (IReadOnlyList<CellValue>)[.. grid.Row(row)])])
            : new ArrayValue([.. Enumerable.Range(0, grid.RowCount).Select(row =>
                (IReadOnlyList<CellValue>)[.. indices.Select(column => grid[row, column])])]);
    }

    private static EvalValue Sort(ValueGrid grid, int keyIndex, bool ascending, bool byColumn)
    {
        if (byColumn)
        {
            if (keyIndex < 0 || keyIndex >= grid.RowCount) return EvalValue.Error(CellError.Value);

            var order = Enumerable.Range(0, grid.ColumnCount)
                .Order(Comparer<int>.Create((a, b) => Compare(grid[keyIndex, a], grid[keyIndex, b]) * (ascending ? 1 : -1)))
                .ToList();

            return new ArrayValue([.. Enumerable.Range(0, grid.RowCount).Select(row =>
                (IReadOnlyList<CellValue>)[.. order.Select(column => grid[row, column])])]);
        }

        if (keyIndex < 0 || keyIndex >= grid.ColumnCount) return EvalValue.Error(CellError.Value);

        var rows = Enumerable.Range(0, grid.RowCount)
            .Order(Comparer<int>.Create((a, b) => Compare(grid[a, keyIndex], grid[b, keyIndex]) * (ascending ? 1 : -1)))
            .ToList();

        return new ArrayValue([.. rows.Select(row => (IReadOnlyList<CellValue>)[.. grid.Row(row)])]);
    }

    private static ArrayValue Transpose(IReadOnlyList<IReadOnlyList<CellValue>> lines)
    {
        var columns = lines.Count == 0 ? 0 : lines.Max(l => l.Count);
        var result = new List<IReadOnlyList<CellValue>>(columns);

        for (var column = 0; column < columns; column++)
            result.Add([.. lines.Select(line =>
                column < line.Count ? line[column] : CellValue.FromError(CellError.NotAvailable))]);

        return new ArrayValue(result);
    }

    private static bool Allocatable(long rows, long columns) =>
        rows <= CellAddress.MaxRows && columns <= CellAddress.MaxColumns && ResultLimits.ArrayFits(rows, columns);

    private static bool IsTrue(CellValue value) =>
        value.TryCoerceToNumber(out var number) && number != 0;

    private readonly LineEquality _lineEquality = new();

    private readonly DelimitedTextSplitter _splitter = new();

    private static List<string> Delimiters(ArgumentReader r, int index) =>
        r.Has(index)
            ? [.. r.Values(index).Where(value => value.Kind != ValueKind.Empty).Select(r.TextOf).Where(text => text.Length > 0)]
            : [];

    private static int Compare(CellValue a, CellValue b)
    {
        var rank = Rank(a).CompareTo(Rank(b));
        if (rank != 0) return rank;
        if (a.IsNumeric) return a.AsNumber().CompareTo(b.AsNumber());
        if (a.Kind == ValueKind.Text) return string.Compare(a.AsText(), b.AsText(), StringComparison.CurrentCultureIgnoreCase);
        return 0;
    }

    private static int Rank(CellValue value) => value.Kind switch
    {
        ValueKind.Number => 0,
        ValueKind.Text => 1,
        ValueKind.Boolean => 2,
        ValueKind.Empty => 3,
        _ => 4,
    };

    private sealed class LineEquality : IEqualityComparer<IReadOnlyList<CellValue>>
    {
        private readonly StringComparer _text = StringComparer.InvariantCultureIgnoreCase;

        public bool Equals(IReadOnlyList<CellValue>? x, IReadOnlyList<CellValue>? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null || x.Count != y.Count) return false;
            for (var i = 0; i < x.Count; i++)
                if (Compare(x[i], y[i]) != 0) return false;
            return true;
        }

        public int GetHashCode(IReadOnlyList<CellValue> line)
        {
            var hash = new HashCode();
            foreach (var value in line)
            {
                hash.Add(Rank(value));
                if (value.IsNumeric) hash.Add(value.AsNumber());
                else if (value.Kind == ValueKind.Text) hash.Add(_text.GetHashCode(value.AsText()));
            }
            return hash.ToHashCode();
        }
    }
}

internal static class CellValueEvalExtensions
{
    public static EvalValue AsEval(this CellValue value) => EvalValue.Scalar(value);
}
