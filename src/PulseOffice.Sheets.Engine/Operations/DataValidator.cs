using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record ValidationOutcome(bool IsValid, DataValidation? Rule)
{
    public static readonly ValidationOutcome Valid = new(true, null);

    public bool Blocks => Reported && Rule?.Alert == ValidationAlert.Stop;

    public bool Reported => !IsValid && Rule?.ShowErrorAlert != false;
}

public interface IDataValidator
{
    DataValidation? RuleAt(Worksheet sheet, CellAddress address);

    ValidationOutcome Validate(CalculationEngine engine, int sheetIndex, CellAddress address, CellValue value);

    IReadOnlyList<string> ChoicesOf(CalculationEngine engine, int sheetIndex, DataValidation rule);
}

public sealed class DataValidator : IDataValidator
{
    private readonly FormulaTranslator _translator = new();

    public DataValidation? RuleAt(Worksheet sheet, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.Validations.LastOrDefault(rule => rule.Range.Contains(address));
    }

    public ValidationOutcome Validate(
        CalculationEngine engine, int sheetIndex, CellAddress address, CellValue value)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var sheet = engine.Workbook.Sheets[sheetIndex];
        var rule = RuleAt(sheet, address);

        if (rule is null || rule.Kind == ValidationKind.Any) return ValidationOutcome.Valid;
        if (value.IsEmpty) return rule.AllowBlank ? ValidationOutcome.Valid : new ValidationOutcome(false, rule);

        var valid = rule.Kind switch
        {
            ValidationKind.List => ChoicesOf(engine, sheetIndex, rule)
                .Contains(Describe(value), StringComparer.CurrentCultureIgnoreCase),

            ValidationKind.TextLength => Compare(engine, sheetIndex, address, rule, Describe(value).Length),

            ValidationKind.WholeNumber => value.TryCoerceToNumber(out var whole)
                && Math.Abs(whole - Math.Round(whole)) < 1e-9
                && Compare(engine, sheetIndex, address, rule, whole),

            ValidationKind.Decimal or ValidationKind.Date or ValidationKind.Time =>
                value.TryCoerceToNumber(out var number) && Compare(engine, sheetIndex, address, rule, number),

            ValidationKind.Custom => IsTrue(engine, sheetIndex, address, rule, rule.FirstFormula),

            _ => true,
        };

        return valid ? ValidationOutcome.Valid : new ValidationOutcome(false, rule);
    }

    public IReadOnlyList<string> ChoicesOf(CalculationEngine engine, int sheetIndex, DataValidation rule)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.ListValues.Count > 0) return rule.ListValues;
        if (string.IsNullOrWhiteSpace(rule.FirstFormula)) return [];

        var values = Evaluate(engine, sheetIndex, rule, rule.FirstFormula, rule.Range.Start);

        return [.. values.Where(value => !value.IsEmpty).Select(Describe)];
    }

    private bool Compare(
        CalculationEngine engine, int sheetIndex, CellAddress address, DataValidation rule, double value)
    {
        var first = Number(engine, sheetIndex, address, rule, rule.FirstFormula);
        var second = Number(engine, sheetIndex, address, rule, rule.SecondFormula);

        if (first is null) return true;

        return rule.Operator switch
        {
            ValidationOperator.Between => second is not null && value >= first && value <= second,
            ValidationOperator.NotBetween => second is not null && (value < first || value > second),
            ValidationOperator.Equal => Math.Abs(value - first.Value) < 1e-9,
            ValidationOperator.NotEqual => Math.Abs(value - first.Value) >= 1e-9,
            ValidationOperator.Greater => value > first,
            ValidationOperator.Less => value < first,
            ValidationOperator.GreaterOrEqual => value >= first,
            _ => value <= first,
        };
    }

    private double? Number(
        CalculationEngine engine, int sheetIndex, CellAddress address, DataValidation rule, string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;

        if (double.TryParse(formula, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var literal)) return literal;

        var values = Evaluate(engine, sheetIndex, rule, formula, address);

        return values.FirstOrDefault().TryCoerceToNumber(out var number) ? number : null;
    }

    private bool IsTrue(
        CalculationEngine engine, int sheetIndex, CellAddress address, DataValidation rule, string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return true;

        var values = Evaluate(engine, sheetIndex, rule, formula, address);
        var first = values.FirstOrDefault();

        return first.Kind switch
        {
            ValueKind.Boolean => first.AsBoolean(),
            ValueKind.Number => Math.Abs(first.AsNumber()) > 1e-12,
            ValueKind.Error => false,
            _ => false,
        };
    }

    private IReadOnlyList<CellValue> Evaluate(
        CalculationEngine engine, int sheetIndex, DataValidation rule, string formula, CellAddress address)
    {
        var written = formula.StartsWith('=') ? formula : "=" + formula;
        var translated = _translator.Translate(
            written, address.Row - rule.Range.Start.Row, address.Column - rule.Range.Start.Column);

        if (translated is null) return [];

        try
        {
            return engine.EvaluateExpression(translated, new SheetCell(sheetIndex, address));
        }
        catch (FormulaSyntaxException)
        {
            return [];
        }
    }

    private string Describe(CellValue value) =>
        value.Kind == ValueKind.Text ? value.AsText() : value.ToString();
}
