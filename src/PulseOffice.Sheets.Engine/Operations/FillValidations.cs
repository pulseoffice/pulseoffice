using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IFillValidations
{
    IReadOnlyList<DataValidation>? Plan(Worksheet sheet, CellRange source, CellRange filled, bool vertical, bool backwards);
}

public sealed class FillValidations(IValidationPlacement placement, IValidationAnchoring anchoring) : IFillValidations
{
    public FillValidations()
        : this(new ValidationPlacement(), new ValidationAnchoring())
    {
    }

    public IReadOnlyList<DataValidation>? Plan(Worksheet sheet, CellRange source, CellRange filled, bool vertical, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var seeds = sheet.Validations.Where(rule => rule.Range.IntersectsWith(source)).Select(rule => Clipped(rule, source)).ToList();
        if (seeds.Count == 0 && !sheet.Validations.Any(rule => rule.Range.IntersectsWith(filled))) return null;

        var placed = placement.Place(sheet.Validations, filled, null).ToList();
        var period = vertical ? source.RowCount : source.ColumnCount;
        foreach (var seed in seeds)
            for (var copy = 1; ; copy++)
            {
                var offset = (backwards ? -1 : 1) * copy * period;
                var shifted = vertical
                    ? Shifted(seed.Range, offset, 0)
                    : Shifted(seed.Range, 0, offset);
                if (shifted is not { } moved || !moved.IntersectsWith(filled)) break;
                var inside = Intersection(moved, filled);
                var anchoredAtCopy = anchoring.Reanchored(seed, moved.Start, moved);
                placed.Add(anchoring.Reanchored(anchoredAtCopy, inside.Start, inside));
            }
        return placed;
    }

    private DataValidation Clipped(DataValidation rule, CellRange source)
    {
        var inside = Intersection(rule.Range, source);
        return anchoring.Reanchored(rule, inside.Start, inside);
    }

    private CellRange Intersection(CellRange a, CellRange b) =>
        new(new CellAddress(Math.Max(a.Start.Row, b.Start.Row), Math.Max(a.Start.Column, b.Start.Column)),
            new CellAddress(Math.Min(a.End.Row, b.End.Row), Math.Min(a.End.Column, b.End.Column)));

    private CellRange? Shifted(CellRange range, int rows, int columns)
    {
        var start = new CellAddress(Math.Max(0, range.Start.Row + rows), Math.Max(0, range.Start.Column + columns));
        var end = new CellAddress(Math.Min(CellAddress.MaxRows - 1, range.End.Row + rows), Math.Min(CellAddress.MaxColumns - 1, range.End.Column + columns));
        if (range.End.Row + rows < 0 || range.End.Column + columns < 0 || range.Start.Row + rows >= CellAddress.MaxRows || range.Start.Column + columns >= CellAddress.MaxColumns) return null;
        return new CellRange(start, end);
    }
}
