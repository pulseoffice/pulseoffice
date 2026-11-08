using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class RangeCutter
{
    public CellRange? Inside(CellRange range, CellRange area)
    {
        if (!range.IntersectsWith(area)) return null;
        return new CellRange(
            new CellAddress(Math.Max(range.Start.Row, area.Start.Row), Math.Max(range.Start.Column, area.Start.Column)),
            new CellAddress(Math.Min(range.End.Row, area.End.Row), Math.Min(range.End.Column, area.End.Column)));
    }

    public IReadOnlyList<CellRange> Outside(CellRange range, CellRange area)
    {
        if (Inside(range, area) is not { } core) return [range];

        var pieces = new List<CellRange>();
        if (range.Start.Row < core.Start.Row)
            pieces.Add(new CellRange(range.Start, new CellAddress(core.Start.Row - 1, range.End.Column)));
        if (range.Start.Column < core.Start.Column)
            pieces.Add(new CellRange(new CellAddress(core.Start.Row, range.Start.Column), new CellAddress(core.End.Row, core.Start.Column - 1)));
        if (core.End.Column < range.End.Column)
            pieces.Add(new CellRange(new CellAddress(core.Start.Row, core.End.Column + 1), new CellAddress(core.End.Row, range.End.Column)));
        if (core.End.Row < range.End.Row)
            pieces.Add(new CellRange(new CellAddress(core.End.Row + 1, range.Start.Column), range.End));
        return pieces;
    }

    public (IReadOnlyList<CellRange> Inside, IReadOnlyList<CellRange> Outside) Split(IEnumerable<CellRange> ranges, CellRange area)
    {
        var inside = new List<CellRange>();
        var outside = new List<CellRange>();
        foreach (var range in ranges)
        {
            if (Inside(range, area) is { } core) inside.Add(core);
            outside.AddRange(Outside(range, area));
        }
        return (inside, outside);
    }
}

internal sealed class RangeMoveSplitter(RangeMoveEdit edit)
{
    private readonly RangeCutter _cutter = new();

    public void SplitRules(Worksheet sheet)
    {
        var rules = sheet.ConditionalRules;
        for (var index = rules.Count - 1; index >= 0; index--)
        {
            var rule = rules[index];
            var (inside, outside) = _cutter.Split(rule.Ranges, edit.Move.Source);
            if (inside.Count == 0 || outside.Count == 0) continue;

            rules[index] = Reanchored(rule, outside);
            rules.Insert(index + 1, Reanchored(rule, inside));
        }
    }

    public void SplitValidations(Worksheet sheet)
    {
        var validations = sheet.Validations;
        for (var index = validations.Count - 1; index >= 0; index--)
        {
            var validation = validations[index];
            if (_cutter.Inside(validation.Range, edit.Move.Source) is not { } core || core == validation.Range) continue;

            validations.RemoveAt(index);
            validations.InsertRange(index, [.. _cutter.Outside(validation.Range, edit.Move.Source).Append(core)
                .Select(piece => Reanchored(validation, piece))]);
        }
    }

    private ConditionalRule Reanchored(ConditionalRule rule, IReadOnlyList<CellRange> ranges)
    {
        return rule with
        {
            Range = ranges[0],
            AdditionalRanges = [.. ranges.Skip(1)],
            Formula = Shifted(rule.Formula, rule.Range.Start, ranges[0].Start),
        };
    }

    private DataValidation Reanchored(DataValidation validation, CellRange range)
    {
        return validation with
        {
            Range = range,
            FirstFormula = Shifted(validation.FirstFormula, validation.Range.Start, range.Start),
            SecondFormula = Shifted(validation.SecondFormula, validation.Range.Start, range.Start),
        };
    }

    private string? Shifted(string? formula, CellAddress from, CellAddress to) =>
        from == to ? formula : edit.Rewrite(new OffsetRewriter(to.Row - from.Row, to.Column - from.Column), formula);
}
