using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record AdvancedFilterRequest(CellRange Data, CellRange Criteria)
{
    public bool HasHeader { get; init; } = true;

    public bool UniqueOnly { get; init; }

    public Worksheet? CriteriaSheet { get; init; }

    public IShiftedFormulaEvaluator? Formulas { get; init; }
}

public interface IShiftedFormulaEvaluator
{
    CellValue EvaluateShifted(Worksheet sheet, CellAddress formula, int rowDelta);
}

public interface ISheetFilter
{
    IReadOnlySet<int> RowsToHide(
        Worksheet sheet, CellRange range, IReadOnlyList<ColumnFilter> filters, bool hasHeader = true, Styling.StyleCatalog? styles = null);

    IReadOnlyList<string> DistinctValues(
        Worksheet sheet, CellRange range, int columnOffset, bool hasHeader = true);

    IReadOnlySet<int> RowsToHide(Worksheet sheet, AdvancedFilterRequest request);

    IReadOnlySet<int> RowsToHide(Worksheet sheet, CellRange range, FilterNode condition, bool hasHeader = true);

    IReadOnlyList<(CellAddress Address, Cell Cell)> CopyMatches(
        Worksheet sheet, AdvancedFilterRequest request, CellAddress output);
}

public sealed class SheetFilterService(NumericText numbers, IFilterRowKey rowKeys) : ISheetFilter
{
    public SheetFilterService(NumericText numbers) : this(numbers, new FilterRowKey())
    {
    }

    private readonly FormulaTranslator _translator = new();

    private readonly CellColorMatcher _colors = new();

    private readonly TopItemsRanking _ranking = new();

    private readonly NumericText _canonical = new(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant());

    private readonly Evaluation.Functions.TextRules _rules = new(CalculationSettings.Default);

    public SheetFilterService() : this(new NumericText(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()))
    {
    }

    public IReadOnlySet<int> RowsToHide(Worksheet sheet, AdvancedFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        var conditions = ReadCriteria(sheet, request);
        var hidden = new HashSet<int>();
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        var firstRow = request.Data.Start.Row + (request.HasHeader ? 1 : 0);

        for (var row = firstRow; row <= request.Data.End.Row; row++)
        {
            var passes = conditions.Count == 0 || conditions.Any(group => group.All(condition => condition.Matches(sheet, row)));

            if (passes && request.UniqueOnly)
            {
                if (!seen.Add(rowKeys.Key(sheet, row, request.Data.Start.Column, request.Data.ColumnCount))) passes = false;
            }

            if (!passes) hidden.Add(row);
        }

        return hidden;
    }

    public IReadOnlyList<(CellAddress Address, Cell Cell)> CopyMatches(
        Worksheet sheet, AdvancedFilterRequest request, CellAddress output)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        var hidden = RowsToHide(sheet, request);
        var result = new List<(CellAddress, Cell)>();
        var target = output.Row;

        for (var row = request.Data.Start.Row; row <= request.Data.End.Row; row++)
        {
            if (hidden.Contains(row)) continue;

            for (var column = request.Data.Start.Column; column <= request.Data.End.Column; column++)
            {
                var source = new CellAddress(row, column);
                var destination = new CellAddress(target, output.Column + column - request.Data.Start.Column);
                if (!destination.IsValid) continue;

                var cell = sheet[source];
                var formula = cell.HasFormula
                    ? _translator.Translate(cell.Formula!, destination.Row - row, destination.Column - column) ?? cell.Formula
                    : null;
                result.Add((destination, Cell.Blank with
                {
                    Value = cell.Value,
                    Formula = formula,
                    StyleId = sheet.StyleIdAt(source),
                    ExplicitStyle = true,
                    Hyperlink = cell.Hyperlink,
                }));
            }

            target++;
        }

        return result;
    }

    private List<List<IRowCondition>> ReadCriteria(Worksheet sheet, AdvancedFilterRequest request)
    {
        var groups = new List<List<IRowCondition>>();
        var headerRow = request.Criteria.Start.Row;
        var firstDataRow = request.Data.Start.Row + (request.HasHeader ? 1 : 0);

        var offsets = new Dictionary<int, int>();
        var computed = new List<int>();
        var criteriaSheet = request.CriteriaSheet ?? sheet;

        for (var column = request.Criteria.Start.Column; column <= request.Criteria.End.Column; column++)
        {
            var name = criteriaSheet[new CellAddress(headerRow, column)].Value.ToString();
            var offset = name.Length == 0 ? -1 : HeaderOffset(sheet, request.Data, name);
            if (offset >= 0) offsets[column] = offset;
            else computed.Add(column);
        }

        for (var row = headerRow + 1; row <= request.Criteria.End.Row; row++)
        {
            var group = new List<IRowCondition>();

            foreach (var (column, offset) in offsets)
            {
                var criteria = criteriaSheet[new CellAddress(row, column)].Value;
                if (criteria.IsEmpty) continue;

                group.Add(new MatchedColumnCondition(request.Data.Start.Column + offset, CriteriaMatcher.Parse(criteria, numbers, _rules)));
            }

            foreach (var column in computed)
            {
                var address = new CellAddress(row, column);
                if (!criteriaSheet[address].HasFormula) continue;

                group.Add(request.Formulas is { } formulas
                    ? new ComputedCondition(formulas, criteriaSheet, address, firstDataRow)
                    : new RejectingCondition());
            }

            if (group.Count > 0) groups.Add(group);
        }

        return groups;
    }

    private int HeaderOffset(Worksheet sheet, CellRange data, string name)
    {
        for (var target = data.Start.Column; target <= data.End.Column; target++)
            if (string.Equals(sheet[new CellAddress(data.Start.Row, target)].Value.ToString(), name, StringComparison.CurrentCultureIgnoreCase))
                return target - data.Start.Column;
        return -1;
    }

    public IReadOnlySet<int> RowsToHide(Worksheet sheet, CellRange range, FilterNode condition, bool hasHeader = true)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(condition);

        var test = Compile(condition, range);
        var hidden = new HashSet<int>();
        for (var row = range.Start.Row + (hasHeader ? 1 : 0); row <= range.End.Row; row++)
            if (!test.Matches(sheet, row)) hidden.Add(row);
        return hidden;
    }

    private IRowCondition Compile(FilterNode node, CellRange range) => node switch
    {
        FilterTerm term when term.Field >= 0 && term.Field < range.ColumnCount =>
            new MatchedColumnCondition(range.Start.Column + term.Field, CriteriaMatcher.Parse(CellValue.Text(term.Criteria), _canonical, _rules)),
        FilterGroup group => new GroupCondition(group.Join, [.. group.Children.Select(child => Compile(child, range))]),
        _ => new RejectingCondition(),
    };

    public IReadOnlySet<int> RowsToHide(
    Worksheet sheet, CellRange range, IReadOnlyList<ColumnFilter> filters, bool hasHeader = true, Styling.StyleCatalog? styles = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(filters);

        var hidden = new HashSet<int>();
        if (filters.Count == 0) return hidden;

        var firstRow = range.Start.Row + (hasHeader ? 1 : 0);
        var ranked = filters.Where(filter => filter.Top is not null).Distinct()
            .ToDictionary(filter => filter, filter => _ranking.Qualifying(sheet, range, firstRow, filter));
        var matchers = filters.Where(filter => filter.Conditions.Count > 0).Distinct()
            .ToDictionary(filter => filter, filter => new ColumnConditions(
                [.. filter.Conditions.Select(criteria => CriteriaMatcher.Parse(CellValue.Text(criteria), _canonical, _rules))], filter.Join));

        for (var row = firstRow; row <= range.End.Row; row++)
        {
            var passes = filters.All(filter => Passes(sheet, range, row, filter, styles, matchers.GetValueOrDefault(filter))
                && (!ranked.TryGetValue(filter, out var qualifying) || qualifying.Contains(row)));
            if (!passes) hidden.Add(row);
        }

        return hidden;
    }

    public IReadOnlyList<string> DistinctValues(
    Worksheet sheet, CellRange range, int columnOffset, bool hasHeader = true)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var firstRow = range.Start.Row + (hasHeader ? 1 : 0);
        var column = range.Start.Column + columnOffset;

        var values = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);

        for (var row = firstRow; row <= range.End.Row; row++)
        {
            var value = sheet[new CellAddress(row, column)].Value;
            if (!value.IsEmpty) values.Add(value.ToString());
        }

        return [.. values];
    }

    private bool Passes(Worksheet sheet, CellRange range, int row, ColumnFilter filter, Styling.StyleCatalog? styles, ColumnConditions? matcher)
    {
        var address = new CellAddress(row, range.Start.Column + filter.ColumnOffset);
        if (filter.Color is { } color && styles is not null && !_colors.Matches(styles.Get(sheet[address].StyleId), color)) return false;
        var value = sheet[address].Value;

        if (value.IsEmpty && !filter.IncludeBlanks) return false;

        if (filter.AllowedValues is { } allowed && !value.IsEmpty && !allowed.Contains(value.ToString()))
            return false;

        return matcher?.Matches(value) ?? true;
    }
}

internal interface IRowCondition
{
    bool Matches(Worksheet sheet, int row);
}

internal sealed class MatchedColumnCondition(int column, CriteriaMatcher matcher) : IRowCondition
{
    public bool Matches(Worksheet sheet, int row) => matcher.Matches(sheet[new CellAddress(row, column)].Value);
}

internal sealed class ComputedCondition(IShiftedFormulaEvaluator formulas, Worksheet criteriaSheet, CellAddress formula, int firstDataRow) : IRowCondition
{
    public bool Matches(Worksheet sheet, int row) => formulas.EvaluateShifted(criteriaSheet, formula, row - firstDataRow) switch
    {
        { Kind: ValueKind.Boolean } value => value.AsBoolean(),
        { Kind: ValueKind.Number } value => value.AsNumber() != 0,
        _ => false,
    };
}

internal sealed class RejectingCondition : IRowCondition
{
    public bool Matches(Worksheet sheet, int row) => false;
}

internal sealed class GroupCondition(FilterGroupJoin join, IReadOnlyList<IRowCondition> children) : IRowCondition
{
    public bool Matches(Worksheet sheet, int row) => join == FilterGroupJoin.Or
        ? children.Any(child => child.Matches(sheet, row))
        : children.All(child => child.Matches(sheet, row));
}

internal sealed class ColumnConditions(IReadOnlyList<CriteriaMatcher> matchers, CriteriaJoin join)
{
    public bool Matches(CellValue value) => join == CriteriaJoin.Or
        ? matchers.Any(matcher => matcher.Matches(value))
        : matchers.All(matcher => matcher.Matches(value));
}

internal sealed class CellColorMatcher
{
    private const string NoFill = "#FFFFFF";

    public bool Matches(Styling.CellStyle style, ColorFilterCriterion criterion) => criterion.Target switch
    {
        ColorFilterTarget.Background => Same(style.BackgroundHex ?? NoFill, criterion.ColorHex ?? NoFill),
        _ => Same(style.Font.ColorHex, criterion.ColorHex ?? Styling.CellFont.Default.ColorHex),
    };

    private bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

internal sealed class TopItemsRanking
{
    public IReadOnlySet<int> Qualifying(Worksheet sheet, CellRange range, int firstRow, ColumnFilter filter)
    {
        var criterion = filter.Top!;
        var column = range.Start.Column + filter.ColumnOffset;
        var numbers = new List<(int Row, double Value)>();
        for (var row = firstRow; row <= range.End.Row; row++)
            if (sheet[new CellAddress(row, column)].Value is { Kind: ValueKind.Number } value) numbers.Add((row, value.AsNumber()));
        if (numbers.Count == 0) return new HashSet<int>();

        var wanted = criterion.Percent
            ? (int)Math.Ceiling(numbers.Count * criterion.Count / 100.0)
            : (int)Math.Floor(criterion.Count);
        wanted = Math.Clamp(wanted, 0, numbers.Count);
        if (wanted == 0) return new HashSet<int>();

        var ordered = criterion.Bottom ? numbers.OrderBy(entry => entry.Value) : numbers.OrderByDescending(entry => entry.Value);
        var threshold = ordered.ElementAt(wanted - 1).Value;
        return numbers.Where(entry => criterion.Bottom ? entry.Value <= threshold : entry.Value >= threshold).Select(entry => entry.Row).ToHashSet();
    }
}
