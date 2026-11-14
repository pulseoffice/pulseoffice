using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class ConditionalFormattingEvaluator
{
    private readonly ConditionalRendition _fallback;
    private readonly IReadOnlyDictionary<ConditionalVisual, ConditionalRendition> _renditions;

    public ConditionalFormattingEvaluator(Evaluation.Evaluator evaluator)
        : this(evaluator, new Evaluation.Functions.Numerics.SystemClock(), new Evaluation.Functions.Numerics.StandardDateSystem())
    {
    }

    public ConditionalFormattingEvaluator(
        Evaluation.Evaluator evaluator, Evaluation.Functions.Numerics.ISystemClock clock, Evaluation.Functions.Numerics.IDateSystem dates)
        : this(evaluator, clock, dates, new SystemSpreadsheetLocale())
    {
    }

    public ConditionalFormattingEvaluator(
        Evaluation.Evaluator evaluator, Evaluation.Functions.Numerics.ISystemClock clock, Evaluation.Functions.Numerics.IDateSystem dates,
        ISpreadsheetLocale locale)
    {
        var snapshots = new ConditionalRangeSnapshots();
        var range = new RangeExtremes(evaluator, snapshots);
        var conditions = new ConditionTests(new DatePeriods(clock, dates), snapshots, new RuleOperandsReader(locale));

        _fallback = new StyleRendition(
            evaluator, conditions);
        _renditions = new ConditionalRendition[] { _fallback, new DataBarRendition(range, new DataBarLayout()), new ColorScaleRendition(range, new ColorRamp()), new IconSetRendition(range) }
            .ToDictionary(rendition => rendition.Visual);
    }

    public ConditionalAppearance Evaluate(
        Worksheet sheet, CellAddress address, Evaluation.IEvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.ConditionalRules.Count == 0 ? ConditionalAppearance.None : Begin(sheet).At(address, context);
    }

    public ConditionalFormattingPass Begin(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return new ConditionalFormattingPass(this, sheet, [.. sheet.ConditionalRules.OrderBy(rule => rule.Priority)]);
    }

    internal ConditionalAppearance Compose(IReadOnlyList<ConditionalRule> ordered, ConditionalCell cell)
    {
        var result = ConditionalAppearance.None;

        foreach (var rule in ordered)
        {
            if (!rule.Covers(cell.Address)) continue;

            var appearance = _renditions.GetValueOrDefault(rule.Visual, _fallback).Render(rule, cell);
            if (appearance.IsEmpty) continue;

            result = new ConditionalAppearance
            {
                Style = result.Style ?? appearance.Style,
                ScaleColorHex = result.ScaleColorHex ?? appearance.ScaleColorHex,
                BarFraction = result.BarFraction ?? appearance.BarFraction,
                BarColorHex = result.BarColorHex ?? appearance.BarColorHex,
                BarShape = result.BarShape ?? appearance.BarShape,
                IconSetName = result.IconSetName ?? appearance.IconSetName,
                IconIndex = result.IconIndex ?? appearance.IconIndex,
                HideValue = appearance.HideValue || result.HideValue,
            };

            if (rule.StopIfTrue) break;
        }

        return result;
    }
}

public sealed class ConditionalFormattingPass
{
    private readonly ConditionalFormattingEvaluator _evaluator;
    private readonly Worksheet _sheet;
    private readonly IReadOnlyList<ConditionalRule> _ordered;
    private readonly Dictionary<CellAddress, ConditionalAppearance> _seen = [];

    internal ConditionalFormattingPass(ConditionalFormattingEvaluator evaluator, Worksheet sheet, IReadOnlyList<ConditionalRule> ordered)
    {
        _evaluator = evaluator;
        _sheet = sheet;
        _ordered = ordered;
    }

    public bool IsEmpty => _ordered.Count == 0;

    public ConditionalAppearance At(CellAddress address, Evaluation.IEvaluationContext? context = null)
    {
        if (_ordered.Count == 0) return ConditionalAppearance.None;
        if (_seen.TryGetValue(address, out var known)) return known;

        var appearance = _evaluator.Compose(_ordered, new ConditionalCell(_sheet, address, _sheet[address].Value, context));
        _seen[address] = appearance;
        return appearance;
    }
}

internal sealed record ConditionalCell(Worksheet Sheet, CellAddress Address, CellValue Value, Evaluation.IEvaluationContext? Context);

internal abstract class ConditionalRendition
{
    public abstract ConditionalVisual Visual { get; }

    public abstract ConditionalAppearance Render(ConditionalRule rule, ConditionalCell cell);
}

internal sealed class StyleRendition(Evaluation.Evaluator evaluator, ConditionTests conditions) : ConditionalRendition
{
    private readonly FormulaTranslator _translator = new();

    public override ConditionalVisual Visual => ConditionalVisual.Style;

    public override ConditionalAppearance Render(ConditionalRule rule, ConditionalCell cell) =>
        Matches(rule, cell) ? new ConditionalAppearance { Style = rule.Style } : ConditionalAppearance.None;

    private bool Matches(ConditionalRule rule, ConditionalCell cell)
    {
        if (rule.Formula is not { Length: > 0 } formula) return conditions.Matches(rule, cell);
        if (cell.Context is null) return false;

        var translated = _translator.Translate(
            formula, cell.Address.Row - rule.Anchor.Row, cell.Address.Column - rule.Anchor.Column) ?? formula;

        try
        {
            return evaluator.Evaluate(new Parsing.FormulaParser().Parse(translated), cell.Context).TryCoerceToNumber(out var number) && number != 0;
        }
        catch (Parsing.FormulaSyntaxException)
        {
            return false;
        }
    }
}

internal sealed class DataBarRendition(RangeExtremes extremes, DataBarLayout layout) : ConditionalRendition
{
    private static readonly ConditionalThreshold[] Defaults =
        [new(ConditionalThresholdKind.Minimum), new(ConditionalThresholdKind.Maximum)];

    public override ConditionalVisual Visual => ConditionalVisual.DataBar;

    public override ConditionalAppearance Render(ConditionalRule rule, ConditionalCell cell)
    {
        if (cell.Value.Kind != ValueKind.Number) return ConditionalAppearance.None;
        if (extremes.Stops(rule, cell, rule.Thresholds.Count >= 2 ? rule.Thresholds : Defaults) is not [var low, .., var high] || high <= low)
            return ConditionalAppearance.None;

        var value = cell.Value.AsNumber();
        var negative = value < 0 && rule.NegativeBarColorHex is not null;
        return new ConditionalAppearance
        {
            BarFraction = Math.Clamp((value - low) / (high - low), 0, 1),
            BarColorHex = negative ? rule.NegativeBarColorHex : rule.BarColorHex,
            BarShape = layout.Shape(rule, value, low, high),
            HideValue = !rule.ShowValue,
        };
    }
}

public sealed class DataBarLayout
{
    public DataBarShape Shape(ConditionalRule rule, double value, double low, double high)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var options = rule.Bar;
        var negative = value < 0;
        var fill = negative ? rule.NegativeBarColorHex ?? rule.BarColorHex : rule.BarColorHex;
        var border = negative ? options.NegativeBorderColorHex ?? options.BorderColorHex : options.BorderColorHex;
        var shortest = Math.Clamp(options.MinimumLength / 100, 0, 1);
        var longest = Math.Clamp(options.MaximumLength / 100, shortest, 1);
        var (start, end, axis) = Span(options.Axis, value, low, high, shortest, longest);
        if (options.Direction == DataBarDirection.RightToLeft)
            (start, end, axis) = (1 - end, 1 - start, axis is { } at ? 1 - at : null);
        return new DataBarShape(start, end, axis, options.Gradient, fill, border, options.AxisColorHex);
    }

    private (double Start, double End, double? Axis) Span(DataBarAxis axisKind, double value, double low, double high, double shortest, double longest)
    {
        var axis = axisKind switch
        {
            DataBarAxis.Middle => 0.5,
            DataBarAxis.Automatic when low < 0 && high > 0 => -low / (high - low),
            _ => (double?)null,
        };
        if (axis is not { } at)
        {
            var share = high > low ? Math.Clamp((value - low) / (high - low), 0, 1) : 0;
            return (0, shortest + (longest - shortest) * share, null);
        }
        if (value >= 0)
        {
            var reach = high > 0 ? Math.Clamp(value / high, 0, 1) : 0;
            return (at, at + (1 - at) * reach * longest, at);
        }
        var depth = low < 0 ? Math.Clamp(value / low, 0, 1) : 0;
        return (at - at * depth * longest, at, at);
    }
}

internal sealed class ColorScaleRendition(RangeExtremes extremes, ColorRamp ramp) : ConditionalRendition
{
    public override ConditionalVisual Visual => ConditionalVisual.ColorScale;

    public override ConditionalAppearance Render(ConditionalRule rule, ConditionalCell cell)
    {
        if (rule.ScaleColors.Count < 2 || cell.Value.Kind != ValueKind.Number) return ConditionalAppearance.None;

        var thresholds = rule.Thresholds.Count == rule.ScaleColors.Count ? rule.Thresholds : Defaults(rule.ScaleColors.Count);
        if (extremes.Stops(rule, cell, thresholds) is not { Count: > 1 } stops) return ConditionalAppearance.None;

        var value = cell.Value.AsNumber();
        if (value <= stops[0]) return new ConditionalAppearance { ScaleColorHex = rule.ScaleColors[0] };
        for (var i = 1; i < stops.Count; i++)
        {
            if (value > stops[i]) continue;
            var span = stops[i] - stops[i - 1];
            var amount = span <= 0 ? 1 : (value - stops[i - 1]) / span;
            return new ConditionalAppearance { ScaleColorHex = ramp.Between(rule.ScaleColors[i - 1], rule.ScaleColors[i], amount) };
        }
        return new ConditionalAppearance { ScaleColorHex = rule.ScaleColors[^1] };
    }

    private IReadOnlyList<ConditionalThreshold> Defaults(int count) =>
        [.. Enumerable.Range(0, count).Select(index =>
            index == 0 ? new ConditionalThreshold(ConditionalThresholdKind.Minimum)
            : index == count - 1 ? new ConditionalThreshold(ConditionalThresholdKind.Maximum)
            : new ConditionalThreshold(ConditionalThresholdKind.Percentile, 100.0 * index / (count - 1)))];
}

internal sealed class IconSetRendition(RangeExtremes extremes) : ConditionalRendition
{
    public override ConditionalVisual Visual => ConditionalVisual.IconSet;

    public override ConditionalAppearance Render(ConditionalRule rule, ConditionalCell cell)
    {
        if (cell.Value.Kind != ValueKind.Number) return ConditionalAppearance.None;

        var count = rule.IconCount;
        var thresholds = rule.Thresholds.Count == count ? rule.Thresholds : Defaults(count);
        if (extremes.Stops(rule, cell, thresholds) is not { Count: > 0 } stops) return ConditionalAppearance.None;

        var value = cell.Value.AsNumber();
        var index = 0;
        for (var i = 1; i < stops.Count; i++)
            if (thresholds[i].GreaterOrEqual ? value >= stops[i] : value > stops[i]) index = i;

        var position = rule.ReverseIcons ? count - 1 - index : index;
        var choice = rule.CustomIcons.Count == count ? rule.CustomIcons[position] : new ConditionalIconChoice(rule.IconSetName, position);
        return choice.IsNone
            ? new ConditionalAppearance { HideValue = !rule.ShowValue }
            : new ConditionalAppearance { IconSetName = choice.Set, IconIndex = choice.Index, HideValue = !rule.ShowValue };
    }

    private IReadOnlyList<ConditionalThreshold> Defaults(int count) =>
        [.. Enumerable.Range(0, count).Select(index => new ConditionalThreshold(ConditionalThresholdKind.Percent, 100.0 * index / count))];
}

internal sealed class RangeExtremes(Evaluation.Evaluator evaluator, ConditionalRangeSnapshots snapshots)
{
    public IReadOnlyList<double>? Stops(ConditionalRule rule, ConditionalCell cell, IReadOnlyList<ConditionalThreshold> thresholds)
    {
        var numbers = snapshots.For(cell.Sheet, rule).Ascending;
        if (numbers.Count == 0) return null;

        var stops = new List<double>(thresholds.Count);
        foreach (var threshold in thresholds)
        {
            if (Stop(threshold, numbers, cell) is not { } stop) return null;
            stops.Add(stop);
        }
        return stops;
    }

    private double? Stop(ConditionalThreshold threshold, List<double> numbers, ConditionalCell cell) => threshold.Kind switch
    {
        ConditionalThresholdKind.Minimum => numbers[0],
        ConditionalThresholdKind.Maximum => numbers[^1],
        ConditionalThresholdKind.AutomaticMinimum => Math.Min(0, numbers[0]),
        ConditionalThresholdKind.AutomaticMaximum => Math.Max(0, numbers[^1]),
        ConditionalThresholdKind.Number => threshold.Value,
        ConditionalThresholdKind.Percent => numbers[0] + (numbers[^1] - numbers[0]) * threshold.Value / 100,
        ConditionalThresholdKind.Percentile => Percentile(numbers, threshold.Value / 100),
        _ => Formula(threshold, cell) ?? threshold.Value,
    };

    private double? Formula(ConditionalThreshold threshold, ConditionalCell cell)
    {
        if (threshold.Formula is not { Length: > 0 } formula || cell.Context is null) return null;
        try
        {
            return evaluator.Evaluate(new Parsing.FormulaParser().Parse(formula), cell.Context)
                .TryCoerceToNumber(out var number) ? number : null;
        }
        catch (Parsing.FormulaSyntaxException)
        {
            return null;
        }
    }

    private double Percentile(List<double> sorted, double fraction)
    {
        var position = Math.Clamp(fraction, 0, 1) * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}

internal sealed class ColorRamp
{
    public string Between(string from, string to, double amount) => Blend(Parse(from), Parse(to), Math.Clamp(amount, 0, 1));

    public string At(IReadOnlyList<string> colors, double position)
    {
        var segments = colors.Count - 1;
        var scaled = position * segments;
        var index = Math.Min((int)scaled, segments - 1);

        return Blend(Parse(colors[index]), Parse(colors[index + 1]), scaled - index);
    }

    private string Blend((int R, int G, int B) from, (int R, int G, int B) to, double amount)
    {
        var r = (int)Math.Round(from.R + (to.R - from.R) * amount);
        var g = (int)Math.Round(from.G + (to.G - from.G) * amount);
        var b = (int)Math.Round(from.B + (to.B - from.B) * amount);

        return string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
    }

    private (int R, int G, int B) Parse(string hex)
    {
        var value = hex.TrimStart('#');
        return value.Length != 6
            ? (0, 0, 0)
            : (Convert.ToInt32(value[..2], 16), Convert.ToInt32(value[2..4], 16), Convert.ToInt32(value[4..], 16));
    }
}

internal sealed class ConditionTests(DatePeriods periods, ConditionalRangeSnapshots snapshots, RuleOperandsReader operands)
{
    private readonly IReadOnlyDictionary<ConditionOperator, ConditionTest> _tests =
        new ConditionTest[]
        {
            new StateTest(operands, ConditionOperator.IsBlank, value => value.IsEmpty),
            new StateTest(operands, ConditionOperator.IsNotBlank, value => !value.IsEmpty),
            new StateTest(operands, ConditionOperator.IsError, value => value.IsError),
            new StateTest(operands, ConditionOperator.IsNotError, value => !value.IsError),
            new TextTest(operands, ConditionOperator.ContainsText, (text, operand) => text.Contains(operand, StringComparison.CurrentCultureIgnoreCase)),
            new TextTest(operands, ConditionOperator.NotContainsText, (text, operand) => !text.Contains(operand, StringComparison.CurrentCultureIgnoreCase)),
            new TextTest(operands, ConditionOperator.BeginsWith, (text, operand) => text.StartsWith(operand, StringComparison.CurrentCultureIgnoreCase)),
            new TextTest(operands, ConditionOperator.EndsWith, (text, operand) => text.EndsWith(operand, StringComparison.CurrentCultureIgnoreCase)),
            new ComparisonTest(operands, ConditionOperator.Equal, order => order == 0),
            new ComparisonTest(operands, ConditionOperator.NotEqual, order => order != 0),
            new ComparisonTest(operands, ConditionOperator.GreaterThan, order => order > 0),
            new ComparisonTest(operands, ConditionOperator.GreaterOrEqual, order => order >= 0),
            new ComparisonTest(operands, ConditionOperator.LessThan, order => order < 0),
            new ComparisonTest(operands, ConditionOperator.LessOrEqual, order => order <= 0),
            new BetweenTest(operands, ConditionOperator.Between, inside: true),
            new BetweenTest(operands, ConditionOperator.NotBetween, inside: false),
            new DuplicateTest(ConditionOperator.Duplicate, duplicated: true, snapshots),
            new DuplicateTest(ConditionOperator.Unique, duplicated: false, snapshots),
            new RankTest(operands, ConditionOperator.TopItems, bottom: false, percent: false, snapshots),
            new RankTest(operands, ConditionOperator.BottomItems, bottom: true, percent: false, snapshots),
            new RankTest(operands, ConditionOperator.TopPercent, bottom: false, percent: true, snapshots),
            new RankTest(operands, ConditionOperator.BottomPercent, bottom: true, percent: true, snapshots),
            new AverageTest(ConditionOperator.AboveAverage, 1, order => order > 0, snapshots),
            new AverageTest(ConditionOperator.BelowAverage, -1, order => order < 0, snapshots),
            new AverageTest(ConditionOperator.AboveOrEqualAverage, 1, order => order >= 0, snapshots),
            new AverageTest(ConditionOperator.BelowOrEqualAverage, -1, order => order <= 0, snapshots),
            new DateTest(periods),
        }.ToDictionary(test => test.Operator);

    public bool Matches(ConditionalRule rule, ConditionalCell cell) =>
        _tests.TryGetValue(rule.Operator, out var test) && test.Matches(rule, cell);
}

internal sealed class RuleOperandsReader(ISpreadsheetLocale locale)
{
    public RuleOperands Read(IReadOnlyList<string> operands) => new(operands, locale.Culture);
}

internal sealed class RuleOperands(IReadOnlyList<string> operands, CultureInfo culture)
{
    public string Text(int index) => index < operands.Count ? operands[index] : string.Empty;

    public bool TryNumber(int index, out double number) =>
        double.TryParse(Text(index).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
        || double.TryParse(Text(index).Trim(), NumberStyles.Float, culture, out number);
}

internal abstract class ConditionTest(ConditionOperator @operator)
{
    public ConditionOperator Operator => @operator;

    public abstract bool Matches(ConditionalRule rule, ConditionalCell cell);
}

internal abstract class OperandTest(RuleOperandsReader reader, ConditionOperator @operator) : ConditionTest(@operator)
{
    public override bool Matches(ConditionalRule rule, ConditionalCell cell) => Matches(reader.Read(rule.Operands), cell.Value);

    protected abstract bool Matches(RuleOperands operands, CellValue value);
}

internal sealed class StateTest(RuleOperandsReader reader, ConditionOperator @operator, Func<CellValue, bool> test) : OperandTest(reader, @operator)
{
    protected override bool Matches(RuleOperands operands, CellValue value) => test(value);
}

internal abstract class ValueTest(RuleOperandsReader reader, ConditionOperator @operator) : OperandTest(reader, @operator)
{
    protected override bool Matches(RuleOperands operands, CellValue value) =>
        !value.IsEmpty && !value.IsError && Present(operands, value);

    protected abstract bool Present(RuleOperands operands, CellValue value);
}

internal sealed class TextTest(RuleOperandsReader reader, ConditionOperator @operator, Func<string, string, bool> test) : ValueTest(reader, @operator)
{
    protected override bool Present(RuleOperands operands, CellValue value) => test(value.ToString(), operands.Text(0));
}

internal sealed class ComparisonTest(RuleOperandsReader reader, ConditionOperator @operator, Func<int, bool> accepts) : ValueTest(reader, @operator)
{
    protected override bool Present(RuleOperands operands, CellValue value) =>
        value.IsNumeric && operands.TryNumber(0, out var first)
            ? accepts(value.AsNumber().CompareTo(first))
            : accepts(string.Compare(value.ToString(), operands.Text(0), StringComparison.CurrentCultureIgnoreCase));
}

internal sealed class BetweenTest(RuleOperandsReader reader, ConditionOperator @operator, bool inside) : ValueTest(reader, @operator)
{
    protected override bool Present(RuleOperands operands, CellValue value)
    {
        if (!value.IsNumeric || !operands.TryNumber(0, out var first) || !operands.TryNumber(1, out var second)) return false;

        var number = value.AsNumber();
        var within = number >= Math.Min(first, second) && number <= Math.Max(first, second);
        return within == inside;
    }
}

internal abstract class RangeTest(ConditionOperator @operator, ConditionalRangeSnapshots snapshots) : ConditionTest(@operator)
{
    protected ConditionalRangeSnapshot Snapshot(ConditionalRule rule, ConditionalCell cell) => snapshots.For(cell.Sheet, rule);
}

internal sealed class DuplicateTest(ConditionOperator @operator, bool duplicated, ConditionalRangeSnapshots snapshots) : RangeTest(@operator, snapshots)
{
    public override bool Matches(ConditionalRule rule, ConditionalCell cell)
    {
        if (cell.Value.IsEmpty || cell.Value.IsError) return false;
        return Snapshot(rule, cell).Occurrences(cell.Value) > 1 == duplicated;
    }
}

internal sealed class RankTest(RuleOperandsReader reader, ConditionOperator @operator, bool bottom, bool percent, ConditionalRangeSnapshots snapshots) : RangeTest(@operator, snapshots)
{
    public override bool Matches(ConditionalRule rule, ConditionalCell cell)
    {
        if (cell.Value.Kind != ValueKind.Number) return false;
        var numbers = Snapshot(rule, cell).Ascending;
        if (numbers.Count == 0) return false;

        var amount = reader.Read(rule.Operands).TryNumber(0, out var parsed) ? parsed : 10;
        var wanted = Math.Clamp(percent ? (int)Math.Ceiling(numbers.Count * amount / 100.0) : (int)Math.Floor(amount), 0, numbers.Count);
        if (wanted == 0) return false;

        var threshold = bottom ? numbers[wanted - 1] : numbers[numbers.Count - wanted];
        var value = cell.Value.AsNumber();
        return bottom ? value <= threshold : value >= threshold;
    }
}

internal sealed class AverageTest(ConditionOperator @operator, int side, Func<int, bool> accepts, ConditionalRangeSnapshots snapshots) : RangeTest(@operator, snapshots)
{
    public override bool Matches(ConditionalRule rule, ConditionalCell cell)
    {
        if (cell.Value.Kind != ValueKind.Number) return false;
        var snapshot = Snapshot(rule, cell);
        if (snapshot.Ascending.Count == 0) return false;
        var bound = snapshot.Average + side * Math.Max(0, rule.StandardDeviations) * snapshot.StandardDeviation;
        return accepts(cell.Value.AsNumber().CompareTo(bound));
    }
}

internal sealed class DateTest(DatePeriods periods) : ConditionTest(ConditionOperator.DateIs)
{
    public override bool Matches(ConditionalRule rule, ConditionalCell cell) =>
        cell.Value.Kind == ValueKind.Number && periods.Contains(rule.DatePeriod, cell.Value.AsNumber());
}

internal sealed class DatePeriods(Evaluation.Functions.Numerics.ISystemClock clock, Evaluation.Functions.Numerics.IDateSystem dates)
{
    public bool Contains(DatePeriod period, double serial)
    {
        var day = DateOnly.FromDateTime(dates.FromSerial(Math.Floor(serial)));
        var today = clock.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var month = new DateOnly(today.Year, today.Month, 1);
        return period switch
        {
            DatePeriod.Today => day == today,
            DatePeriod.Yesterday => day == today.AddDays(-1),
            DatePeriod.Tomorrow => day == today.AddDays(1),
            DatePeriod.Last7Days => day > today.AddDays(-7) && day <= today,
            DatePeriod.ThisWeek => Within(day, weekStart, 7),
            DatePeriod.LastWeek => Within(day, weekStart.AddDays(-7), 7),
            DatePeriod.NextWeek => Within(day, weekStart.AddDays(7), 7),
            DatePeriod.ThisMonth => SameMonth(day, month),
            DatePeriod.LastMonth => SameMonth(day, month.AddMonths(-1)),
            DatePeriod.NextMonth => SameMonth(day, month.AddMonths(1)),
            DatePeriod.ThisYear => day.Year == today.Year,
            DatePeriod.LastYear => day.Year == today.Year - 1,
            _ => day.Year == today.Year + 1,
        };
    }

    private bool Within(DateOnly day, DateOnly start, int length) => day >= start && day < start.AddDays(length);

    private bool SameMonth(DateOnly day, DateOnly month) => day.Year == month.Year && day.Month == month.Month;
}
