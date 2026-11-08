using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IValidationPlacement
{
    IReadOnlyList<DataValidation> Place(IReadOnlyList<DataValidation> existing, CellRange area, DataValidation? rule);
}

public sealed class ValidationPlacement : IValidationPlacement
{
    private readonly RangeCutter _cutter = new();
    private readonly FormulaTranslator _translator = new();

    public IReadOnlyList<DataValidation> Place(IReadOnlyList<DataValidation> existing, CellRange area, DataValidation? rule)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var placed = new List<DataValidation>();
        foreach (var validation in existing)
        {
            if (!validation.Range.IntersectsWith(area))
            {
                placed.Add(validation);
                continue;
            }
            placed.AddRange(_cutter.Outside(validation.Range, area).Select(piece => Reanchored(validation, piece)));
        }
        if (rule is not null) placed.Add(rule);
        return placed;
    }

    private DataValidation Reanchored(DataValidation validation, CellRange range) =>
        range.Start == validation.Range.Start
            ? validation with { Range = range }
            : validation with
            {
                Range = range,
                FirstFormula = Shifted(validation.FirstFormula, validation.Range.Start, range.Start),
                SecondFormula = Shifted(validation.SecondFormula, validation.Range.Start, range.Start),
            };

    private string? Shifted(string? formula, CellAddress from, CellAddress to)
    {
        if (string.IsNullOrEmpty(formula)) return formula;

        var prefixed = formula.StartsWith('=');
        var moved = _translator.Translate(prefixed ? formula : "=" + formula, to.Row - from.Row, to.Column - from.Column);
        return moved is null ? formula : prefixed ? moved : moved.TrimStart('=');
    }
}
