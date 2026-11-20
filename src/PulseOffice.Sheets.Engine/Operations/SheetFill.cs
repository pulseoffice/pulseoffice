using System.Globalization;
using System.Text.RegularExpressions;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public enum FillDirection { Down, Up, Right, Left }

public enum FillMode { Automatic, Copy, Series, FormattingOnly, WithoutFormatting }

public sealed record FillMergePlan(IReadOnlyList<CellRange> Removed, IReadOnlyList<CellRange> Added)
{
    public static FillMergePlan None { get; } = new([], []);

    public bool IsEmpty => Removed.Count == 0 && Added.Count == 0;
}

public interface ISheetFill
{
    IReadOnlyList<(CellAddress Address, Cell Cell)> Extend(Worksheet sheet, CellRange source, CellRange target, FillDirection direction);

    IReadOnlyList<(CellAddress Address, Cell Cell)> Extend(Worksheet sheet, CellRange source, CellRange target, FillDirection direction, FillMode mode);

    FillMode Toggled(Worksheet sheet, CellRange source, FillDirection direction);

    FillMergePlan Merges(Worksheet sheet, CellRange source, CellRange target, FillDirection direction);

    IReadOnlyList<(CellAddress Address, CellComment? Comment)> Comments(Worksheet sheet, CellRange source, CellRange target, FillDirection direction);

    IReadOnlyList<DataValidation>? Validations(Worksheet sheet, CellRange source, CellRange target, FillDirection direction);
}

public sealed class SheetFill : ISheetFill
{
    private readonly IReadOnlyDictionary<FillDirection, FillAxis> _axes;
    private readonly IReadOnlyList<FillRule> _rules;
    private readonly IReadOnlyList<FillRule> _copyRules;

    private readonly PulseOffice.Sheets.Formatting.ICalendarFormats _calendars;

    private readonly FillValidations _validations = new();

    public SheetFill() : this(new PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale(), new PulseOffice.Sheets.Formatting.CalendarFormats())
    {
    }

    public SheetFill(PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale, PulseOffice.Sheets.Formatting.ICalendarFormats calendars)
    {
        ArgumentNullException.ThrowIfNull(locale);
        _calendars = calendars ?? throw new ArgumentNullException(nameof(calendars));
        _axes = new FillAxis[] { new DownAxis(), new UpAxis(), new RightAxis(), new LeftAxis() }.ToDictionary(axis => axis.Direction);
        _rules = [new FormulaFillRule(), new ProgressionFillRule(), new SingleDateFillRule(), new NamedListFillRule(locale), new NumberedTextFillRule(), new SingleNumberSeriesFillRule(), new RepeatFillRule()];
        _copyRules = [new FormulaFillRule(), new RepeatFillRule()];
    }

    public FillMode Toggled(Worksheet sheet, CellRange source, FillDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var laneLength = direction is FillDirection.Down or FillDirection.Up ? source.RowCount : source.ColumnCount;
        if (laneLength != 1) return FillMode.Copy;
        var numbers = 0;
        for (var row = source.Start.Row; row <= source.End.Row; row++)
            for (var column = source.Start.Column; column <= source.End.Column; column++)
            {
                var address = new CellAddress(row, column);
                if (sheet[address] is not { } cell || (!cell.HasFormula && cell.Value.Kind == ValueKind.Empty)) continue;
                if (cell.HasFormula || cell.Value.Kind != ValueKind.Number
                    || (sheet.Owner is { } owner && _calendars.NamesDays(owner, sheet.StyleIdAt(address))))
                    return FillMode.Copy;
                numbers++;
            }
        return numbers > 0 ? FillMode.Series : FillMode.Copy;
    }

    public IReadOnlyList<(CellAddress Address, Cell Cell)> Extend(
        Worksheet sheet, CellRange source, CellRange target, FillDirection direction) =>
        Extend(sheet, source, target, direction, FillMode.Automatic);

    public IReadOnlyList<(CellAddress Address, Cell Cell)> Extend(
        Worksheet sheet, CellRange source, CellRange target, FillDirection direction, FillMode mode)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var rules = mode == FillMode.Copy ? _copyRules : _rules;

        var axis = _axes[direction];
        var result = new List<(CellAddress, Cell)>();
        var steps = axis.Steps(source, target);

        for (var lane = 0; lane < axis.Lanes(source); lane++)
        {
            var seed = axis.Seed(source, lane).Select(address => sheet[address]).ToList();
            if (seed.Count == 0) continue;
            var singleDate = seed.Count == 1 && !seed[0].HasFormula && seed[0].Value.Kind == ValueKind.Number
                && sheet.Owner is { } owner && _calendars.NamesDays(owner, sheet.StyleIdAt(axis.Seed(source, lane).First()));

            var visible = 0;
            for (var position = 1; position <= steps; position++)
            {
                var address = axis.Target(source, lane, position);
                if (!address.IsValid || axis.Hidden(sheet, address)) continue;

                visible++;
                var fill = new FillStep(seed, visible, address, axis.Origin(source, lane, visible), axis.Backwards) { SingleDate = singleDate, Mode = mode };
                var produced = rules.Select(rule => rule.Produce(fill)).First(cell => cell is not null)!;
                var previous = sheet[address];
                result.Add((address, mode switch
                {
                    FillMode.FormattingOnly => previous with { StyleId = fill.Template.StyleId, ExplicitStyle = fill.Template.ExplicitStyle },
                    FillMode.WithoutFormatting => produced with { StyleId = previous.StyleId, ExplicitStyle = previous.ExplicitStyle },
                    _ => produced,
                }));
            }
        }

        return result;
    }

    public IReadOnlyList<(CellAddress Address, CellComment? Comment)> Comments(
        Worksheet sheet, CellRange source, CellRange target, FillDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var axis = _axes[direction];
        var result = new List<(CellAddress, CellComment?)>();
        var steps = axis.Steps(source, target);

        for (var lane = 0; lane < axis.Lanes(source); lane++)
        {
            var seed = axis.Seed(source, lane).Select(address => sheet.Comments.GetValueOrDefault(address)).ToList();
            if (seed.Count == 0) continue;

            var visible = 0;
            for (var position = 1; position <= steps; position++)
            {
                var address = axis.Target(source, lane, position);
                if (!address.IsValid || axis.Hidden(sheet, address)) continue;

                visible++;
                var index = (visible - 1) % seed.Count;
                var comment = seed[axis.Backwards ? seed.Count - 1 - index : index];
                if (!Equals(sheet.Comments.GetValueOrDefault(address), comment)) result.Add((address, comment));
            }
        }

        return result;
    }

    public IReadOnlyList<DataValidation>? Validations(Worksheet sheet, CellRange source, CellRange target, FillDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var axis = _axes[direction];
        var steps = axis.Steps(source, target);
        return steps <= 0 ? null : _validations.Plan(sheet, source, axis.Filled(source, steps), axis.Vertical, axis.Backwards);
    }

    public FillMergePlan Merges(Worksheet sheet, CellRange source, CellRange target, FillDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var axis = _axes[direction];
        var steps = axis.Steps(source, target);
        if (steps <= 0) return FillMergePlan.None;
        for (var position = 1; position <= steps; position++)
            if (axis.Hidden(sheet, axis.Target(source, 0, position))) return FillMergePlan.None;

        var period = axis.Vertical ? source.RowCount : source.ColumnCount;
        var filled = axis.Filled(source, steps);
        var result = new List<CellRange>();

        foreach (var region in sheet.Merges.Overlapping(source).Where(region => source.Contains(region.Start) && source.Contains(region.End)))
            for (var copy = 1; ; copy++)
            {
                var offset = (axis.Backwards ? -1 : 1) * copy * period;
                var shifted = axis.Vertical
                    ? new CellRange(new CellAddress(region.Start.Row + offset, region.Start.Column), new CellAddress(region.End.Row + offset, region.End.Column))
                    : new CellRange(new CellAddress(region.Start.Row, region.Start.Column + offset), new CellAddress(region.End.Row, region.End.Column + offset));
                if (!shifted.Start.IsValid || !shifted.End.IsValid) break;
                if (!filled.Contains(shifted.Start) || !filled.Contains(shifted.End)) break;
                result.Add(shifted);
            }

        return new FillMergePlan([.. sheet.Merges.Overlapping(filled)], result);
    }
}

internal sealed record FillStep(IReadOnlyList<Cell> Seed, int Step, CellAddress Address, CellAddress Origin, bool Backwards)
{
    public bool SingleDate { get; init; }

    public FillMode Mode { get; init; }

    public int Cycle => (Step - 1) / Seed.Count + 1;

    public Cell Template => Seed[Backwards ? Seed.Count - 1 - ((Step - 1) % Seed.Count) : (Step - 1) % Seed.Count];
}

internal abstract class FillAxis
{
    public abstract FillDirection Direction { get; }

    public abstract bool Vertical { get; }

    public bool Backwards => Direction is FillDirection.Up or FillDirection.Left;

    public int Lanes(CellRange source) => Vertical ? source.ColumnCount : source.RowCount;

    public IEnumerable<CellAddress> Seed(CellRange source, int lane)
    {
        var length = Vertical ? source.RowCount : source.ColumnCount;
        for (var i = 0; i < length; i++)
            yield return Vertical
                ? new CellAddress(source.Start.Row + i, source.Start.Column + lane)
                : new CellAddress(source.Start.Row + lane, source.Start.Column + i);
    }

    public abstract int Steps(CellRange source, CellRange target);

    public bool Hidden(Worksheet sheet, CellAddress address) =>
        Vertical ? sheet.Layout.IsRowHidden(address.Row) : sheet.Layout.IsColumnHidden(address.Column);

    public CellRange Filled(CellRange source, int steps) => new(Target(source, 0, 1), Target(source, Lanes(source) - 1, steps));

    public abstract CellAddress Target(CellRange source, int lane, int step);

    public abstract CellAddress Origin(CellRange source, int lane, int step);
}

internal sealed class DownAxis : FillAxis
{
    public override FillDirection Direction => FillDirection.Down;
    public override bool Vertical => true;
    public override int Steps(CellRange source, CellRange target) => target.End.Row - source.End.Row;
    public override CellAddress Target(CellRange source, int lane, int step) => new(source.End.Row + step, source.Start.Column + lane);
    public override CellAddress Origin(CellRange source, int lane, int step) =>
        new(source.End.Row - (source.RowCount - 1 - (step - 1) % source.RowCount), source.Start.Column + lane);
}

internal sealed class UpAxis : FillAxis
{
    public override FillDirection Direction => FillDirection.Up;
    public override bool Vertical => true;
    public override int Steps(CellRange source, CellRange target) => source.Start.Row - target.Start.Row;
    public override CellAddress Target(CellRange source, int lane, int step) => new(source.Start.Row - step, source.Start.Column + lane);
    public override CellAddress Origin(CellRange source, int lane, int step) =>
        new(source.End.Row - (step - 1) % source.RowCount, source.Start.Column + lane);
}

internal sealed class RightAxis : FillAxis
{
    public override FillDirection Direction => FillDirection.Right;
    public override bool Vertical => false;
    public override int Steps(CellRange source, CellRange target) => target.End.Column - source.End.Column;
    public override CellAddress Target(CellRange source, int lane, int step) => new(source.Start.Row + lane, source.End.Column + step);
    public override CellAddress Origin(CellRange source, int lane, int step) =>
        new(source.Start.Row + lane, source.End.Column - (source.ColumnCount - 1 - (step - 1) % source.ColumnCount));
}

internal sealed class LeftAxis : FillAxis
{
    public override FillDirection Direction => FillDirection.Left;
    public override bool Vertical => false;
    public override int Steps(CellRange source, CellRange target) => source.Start.Column - target.Start.Column;
    public override CellAddress Target(CellRange source, int lane, int step) => new(source.Start.Row + lane, source.Start.Column - step);
    public override CellAddress Origin(CellRange source, int lane, int step) =>
        new(source.Start.Row + lane, source.End.Column - (step - 1) % source.ColumnCount);
}

internal abstract class FillRule
{
    public abstract Cell? Produce(FillStep step);
}

internal sealed class FormulaFillRule : FillRule
{
    private readonly FormulaTranslator _translator = new();

    public override Cell? Produce(FillStep step) =>
        step.Template.HasFormula
            ? step.Template with
            {
                Formula = _translator.Translate(
                    step.Template.Formula!, step.Address.Row - step.Origin.Row, step.Address.Column - step.Origin.Column),
            }
            : null;
}

internal sealed class ProgressionFillRule : FillRule
{
    public override Cell? Produce(FillStep step)
    {
        if (Progression(step.Seed) is not var (first, delta)) return null;

        var position = step.Backwards ? -step.Step : step.Seed.Count - 1 + step.Step;
        return step.Template with { Value = CellValue.Number(first + delta * position), Formula = null };
    }

    private (double First, double Delta)? Progression(IReadOnlyList<Cell> seed)
    {
        if (seed.Count < 2) return null;
        if (seed.Any(cell => cell.HasFormula || cell.Value.Kind != ValueKind.Number)) return null;

        var numbers = seed.Select(cell => cell.Value.AsNumber()).ToArray();
        var delta = numbers[1] - numbers[0];

        for (var i = 2; i < numbers.Length; i++)
            if (Math.Abs(numbers[i] - numbers[i - 1] - delta) > 1e-9)
                return null;

        return (numbers[0], delta);
    }
}

internal sealed class SingleDateFillRule : FillRule
{
    public override Cell? Produce(FillStep step) =>
        step.SingleDate
            ? step.Template with { Value = CellValue.Number(step.Template.Value.AsNumber() + (step.Backwards ? -step.Step : step.Step)), Formula = null }
            : null;
}

internal sealed class NamedListFillRule(PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale) : FillRule
{
    public override Cell? Produce(FillStep step)
    {
        var template = step.Template;
        if (template.HasFormula || template.Value.Kind != ValueKind.Text) return null;
        var text = template.Value.AsText().Trim();
        if (text.Length == 0) return null;

        foreach (var list in Lists())
        {
            var index = Array.FindIndex(list, item => string.Equals(item, text, StringComparison.CurrentCultureIgnoreCase));
            if (index < 0) continue;
            var increment = step.Cycle * step.Seed.Count;
            var next = list[((index + (step.Backwards ? -increment : increment)) % list.Length + list.Length) % list.Length];
            return template with { Value = CellValue.Text(Cased(text, next)), Formula = null };
        }

        return null;
    }

    private IEnumerable<string[]> Lists()
    {
        foreach (var culture in new[] { locale.Culture, CultureInfo.InvariantCulture }.Distinct())
        {
            var format = culture.DateTimeFormat;
            yield return format.DayNames;
            yield return format.AbbreviatedDayNames;
            yield return [.. format.MonthNames.Where(name => name.Length > 0)];
            yield return [.. format.AbbreviatedMonthNames.Where(name => name.Length > 0)];
        }
    }

    private string Cased(string typed, string next) =>
        typed.Any(char.IsLetter) && typed.Where(char.IsLetter).All(char.IsUpper) ? next.ToUpper(locale.Culture)
        : typed.Length > 0 && char.IsUpper(typed[0]) ? char.ToUpper(next[0], locale.Culture) + next[1..]
        : typed.Where(char.IsLetter).All(char.IsLower) ? next.ToLower(locale.Culture)
        : next;
}

internal sealed class NumberedTextFillRule : FillRule
{
    private readonly Regex _trailingNumber = new(
        @"^(?<prefix>.*?)(?<number>[0-9]{1,18})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public override Cell? Produce(FillStep step)
    {
        var template = step.Template;
        if (template.Value.Kind != ValueKind.Text || _trailingNumber.Match(template.Value.AsText()) is not { Success: true } match)
            return null;

        var digits = match.Groups["number"].Value;
        var prefix = match.Groups["prefix"].Value;
        if (prefix.Length > 0 && char.IsAsciiDigit(prefix[^1])) return null;
        var number = long.Parse(digits, CultureInfo.InvariantCulture);
        var increment = (long)step.Cycle * Stride(step, prefix);
        var next = Math.Max(0, number + (step.Backwards ? -increment : increment));

        var text = match.Groups["prefix"].Value + next.ToString(CultureInfo.InvariantCulture).PadLeft(digits.Length, '0');
        return template with { Value = CellValue.Text(text), Formula = null };
    }

    private long Stride(FillStep step, string prefix)
    {
        if (step.Seed.Count < 2) return step.Seed.Count;
        var numbers = new List<long>();
        foreach (var cell in step.Seed)
        {
            if (cell.HasFormula || cell.Value.Kind != ValueKind.Text
                || _trailingNumber.Match(cell.Value.AsText()) is not { Success: true } match
                || !string.Equals(match.Groups["prefix"].Value, prefix, StringComparison.Ordinal)
                || match.Groups["number"].Value.Length > 18)
                return step.Seed.Count;
            numbers.Add(long.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture));
        }

        var difference = numbers[1] - numbers[0];
        for (var index = 2; index < numbers.Count; index++)
            if (numbers[index] - numbers[index - 1] != difference) return step.Seed.Count;
        return difference * step.Seed.Count;
    }
}

internal sealed class SingleNumberSeriesFillRule : FillRule
{
    public override Cell? Produce(FillStep step) =>
        step.Mode == FillMode.Series && step.Seed.Count == 1 && !step.Template.HasFormula && step.Template.Value.Kind == ValueKind.Number
            ? step.Template with { Value = CellValue.Number(step.Template.Value.AsNumber() + (step.Backwards ? -step.Step : step.Step)), Formula = null }
            : null;
}

internal sealed class RepeatFillRule : FillRule
{
    public override Cell? Produce(FillStep step) => step.Template;
}
