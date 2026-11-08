using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class ValidationFacade(IScriptDispatcher dispatcher, IActiveSheetContext context, IScriptSheetEdits edits, IScriptReferences references, IScriptProtection protection, PulseOffice.Sheets.Engine.Operations.IValidationPlacement placement) : ScriptFacade(dispatcher)
{
    public int Count => Read(() => Sheet()?.Validations.Count ?? 0);

    public string[] Describe() => Read<string[]>(() =>
        Sheet() is { } sheet
            ? [.. sheet.Validations.Select(rule => rule.Kind == ValidationKind.List
                ? $"{rule.Range} {rule.Kind} {(rule.ListValues.Count > 0 ? string.Join(";", rule.ListValues) : rule.FirstFormula)}".Trim()
                : $"{rule.Range} {rule.Kind} {rule.Operator} {rule.FirstFormula} {rule.SecondFormula}".Trim())]
            : []);

    public bool AddList(
        string reference, string[] values, bool allowBlank = true, string alert = "Stop") =>
        Add(reference, rule => rule with
        {
            Kind = ValidationKind.List,
            ListValues = values ?? [],
            AllowBlank = allowBlank,
            Alert = Alert(alert),
        });

    public bool AddWholeNumber(
        string reference, double minimum, double maximum, string alert = "Stop") =>
        Add(reference, rule => rule with
        {
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Between,
            FirstFormula = Text(minimum, maximum, minimum),
            SecondFormula = Text(minimum, maximum, maximum),
            Alert = Alert(alert),
        });

    public bool AddDecimal(
        string reference, double minimum, double maximum, string alert = "Stop") =>
        Add(reference, rule => rule with
        {
            Kind = ValidationKind.Decimal,
            Operator = ValidationOperator.Between,
            FirstFormula = Text(minimum, maximum, minimum),
            SecondFormula = Text(minimum, maximum, maximum),
            Alert = Alert(alert),
        });

    public bool AddTextLength(
        string reference, double minimum, double maximum, string alert = "Stop") =>
        Add(reference, rule => rule with
        {
            Kind = ValidationKind.TextLength,
            Operator = ValidationOperator.Between,
            FirstFormula = Text(minimum, maximum, minimum),
            SecondFormula = Text(minimum, maximum, maximum),
            Alert = Alert(alert),
        });

    public bool AddDate(string reference, string first, string second, string alert = "Stop") =>
        Add(reference, rule => rule with
        {
            Kind = ValidationKind.Date,
            Operator = ValidationOperator.Between,
            FirstFormula = first,
            SecondFormula = second,
            Alert = Alert(alert),
        });

    public bool AddFormula(string reference, string formula, string alert = "Stop") =>
        Add(reference, rule => rule with
        {
            Kind = ValidationKind.Custom,
            FirstFormula = Formula(formula),
            Alert = Alert(alert),
        });

    public bool Message(string reference, string title, string message) => Write(() => edits.Record("Validation", () =>
    {
        var range = references.Range(reference);
        protection.Unprotected("changing validity rules");
        if (Sheet() is not { } sheet) return false;

        var index = sheet.Validations.FindIndex(rule => rule.Range == range);

        if (index < 0) throw new ArgumentException($"There is no validation on {range}: add one first.", nameof(reference));

        sheet.Validations[index] = sheet.Validations[index] with
        {
            InputTitle = title,
            InputMessage = message,
        };

        return true;
    }, done => done));

    public int Clear(string? reference = null) => Write(() => edits.Record("Validation", () =>
    {
        var scope = references.Scope(reference);
        protection.Unprotected("changing validity rules");
        if (Sheet() is not { } sheet) return 0;

        var removed = scope is { } range
            ? sheet.Validations.Count(rule => rule.Range.IntersectsWith(range))
            : sheet.Validations.Count;
        var kept = scope is { } area ? placement.Place(sheet.Validations, area, null) : [];
        sheet.Validations.Clear();
        sheet.Validations.AddRange(kept);

        context.RequestRefresh();

        return removed;
    }, count => count > 0));

    private bool Add(string reference, Func<DataValidation, DataValidation> build) =>
        Write(() => edits.Record("Validation", () =>
        {
            var range = references.Range(reference);
            protection.Unprotected("changing validity rules");
            if (Sheet() is not { } sheet) return false;

            var added = build(new DataValidation { Range = range });
            var placed = placement.Place(sheet.Validations, range, added);
            sheet.Validations.Clear();
            sheet.Validations.AddRange(placed);

            context.RequestRefresh();

            return true;
        }, done => done));

    private ValidationAlert Alert(string alert) => (alert ?? string.Empty).ToLowerInvariant() switch
    {
        "stop" or "" => ValidationAlert.Stop,
        "warning" or "avviso" => ValidationAlert.Warning,
        "information" or "informazione" => ValidationAlert.Information,
        _ => throw new ArgumentException($"Unknown alert '{alert}': use Stop, Warning or Information.", nameof(alert)),
    };

    private string Text(double minimum, double maximum, double value)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum))
            throw new ArgumentOutOfRangeException(nameof(minimum), "The limits must be numbers.");
        if (minimum > maximum)
            throw new ArgumentException($"The minimum {minimum} is greater than the maximum {maximum}.", nameof(minimum));
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string Formula(string formula) => !string.IsNullOrWhiteSpace(formula) && formula != "="
        ? formula.StartsWith('=') ? formula : "=" + formula
        : throw new ArgumentException("The formula is empty.", nameof(formula));

    private Worksheet? Sheet() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex] : null;
}
