using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.Dialogs;

public sealed record ValidationDraft
{
    public ValidationKind Kind { get; init; } = ValidationKind.Any;

    public ValidationOperator Operator { get; init; } = ValidationOperator.Between;

    public string First { get; init; } = string.Empty;

    public string Second { get; init; } = string.Empty;

    public IReadOnlyList<string> ListValues { get; init; } = [];

    public bool ListFromRange { get; init; }

    public string ListRange { get; init; } = string.Empty;

    public bool AllowBlank { get; init; } = true;

    public bool ShowDropdown { get; init; } = true;

    public string InputTitle { get; init; } = string.Empty;

    public string InputMessage { get; init; } = string.Empty;

    public bool ShowInputMessage { get; init; } = true;

    public bool ShowErrorAlert { get; init; } = true;

    public ValidationAlert Alert { get; init; } = ValidationAlert.Stop;

    public string ErrorTitle { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed record ValidationLayout(
    bool Operator, string? FirstLabelKey, string? SecondLabelKey, bool List, bool Formula, bool Dropdown);

public interface IValidationRules
{
    IReadOnlyList<ValidationKind> Kinds { get; }

    IReadOnlyList<ValidationOperator> Operators { get; }

    ValidationLayout Layout(ValidationKind kind, ValidationOperator op);

    ValidationDraft From(DataValidation? rule);

    DataValidation? Build(ValidationDraft draft, CellRange range);
}

public sealed class ValidationRules : IValidationRules
{
    public IReadOnlyList<ValidationKind> Kinds { get; } =
    [
        ValidationKind.Any, ValidationKind.WholeNumber, ValidationKind.Decimal, ValidationKind.List,
        ValidationKind.Date, ValidationKind.Time, ValidationKind.TextLength, ValidationKind.Custom,
    ];

    public IReadOnlyList<ValidationOperator> Operators { get; } =
    [
        ValidationOperator.Between, ValidationOperator.NotBetween, ValidationOperator.Equal,
        ValidationOperator.NotEqual, ValidationOperator.Greater, ValidationOperator.Less,
        ValidationOperator.GreaterOrEqual, ValidationOperator.LessOrEqual,
    ];

    public ValidationLayout Layout(ValidationKind kind, ValidationOperator op) => kind switch
    {
        ValidationKind.Any => new(false, null, null, false, false, false),
        ValidationKind.List => new(false, null, null, true, false, true),
        ValidationKind.Custom => new(false, "validation.formula", null, false, true, false),
        _ when Ranged(op) => new(true, Minimum(kind), Maximum(kind), false, false, false),
        _ => new(true, Single(kind), null, false, false, false),
    };

    public ValidationDraft From(DataValidation? rule)
    {
        if (rule is null) return new ValidationDraft();

        var fromRange = rule.Kind == ValidationKind.List && rule.ListValues.Count == 0 && rule.FirstFormula is { Length: > 0 };

        return new ValidationDraft
        {
            Kind = rule.Kind,
            Operator = rule.Operator,
            First = fromRange ? string.Empty : rule.FirstFormula ?? string.Empty,
            Second = rule.SecondFormula ?? string.Empty,
            ListValues = rule.ListValues,
            ListFromRange = fromRange,
            ListRange = fromRange ? rule.FirstFormula! : string.Empty,
            AllowBlank = rule.AllowBlank,
            ShowDropdown = rule.ShowDropdown,
            InputTitle = rule.InputTitle ?? string.Empty,
            InputMessage = rule.InputMessage ?? string.Empty,
            ShowInputMessage = rule.ShowInputMessage,
            ShowErrorAlert = rule.ShowErrorAlert,
            Alert = rule.Alert,
            ErrorTitle = rule.ErrorTitle ?? string.Empty,
            ErrorMessage = rule.ErrorMessage ?? string.Empty,
        };
    }

    public DataValidation? Build(ValidationDraft draft, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.Kind == ValidationKind.Any) return null;

        var layout = Layout(draft.Kind, draft.Operator);
        var list = draft.Kind == ValidationKind.List;

        return new DataValidation
        {
            Range = range,
            Kind = draft.Kind,
            Operator = layout.Operator ? draft.Operator : ValidationOperator.Between,
            FirstFormula = list
                ? (draft.ListFromRange ? Blank(draft.ListRange) : null)
                : layout.FirstLabelKey is null ? null : Blank(draft.First),
            SecondFormula = layout.SecondLabelKey is null ? null : Blank(draft.Second),
            ListValues = list && !draft.ListFromRange
                ? [.. draft.ListValues.Select(value => value.Trim()).Where(value => value.Length > 0)]
                : [],
            AllowBlank = draft.AllowBlank,
            ShowDropdown = !list || draft.ShowDropdown,
            InputTitle = Blank(draft.InputTitle),
            InputMessage = Blank(draft.InputMessage),
            ShowInputMessage = draft.ShowInputMessage,
            ShowErrorAlert = draft.ShowErrorAlert,
            Alert = draft.Alert,
            ErrorTitle = Blank(draft.ErrorTitle),
            ErrorMessage = Blank(draft.ErrorMessage),
        };
    }

    private bool Ranged(ValidationOperator op) => op is ValidationOperator.Between or ValidationOperator.NotBetween;

    private string Minimum(ValidationKind kind) => kind switch
    {
        ValidationKind.Date => "validation.dateFrom",
        ValidationKind.Time => "validation.timeFrom",
        ValidationKind.TextLength => "validation.lengthMinimum",
        _ => "validation.minimum",
    };

    private string Maximum(ValidationKind kind) => kind switch
    {
        ValidationKind.Date => "validation.dateTo",
        ValidationKind.Time => "validation.timeTo",
        ValidationKind.TextLength => "validation.lengthMaximum",
        _ => "validation.maximum",
    };

    private string Single(ValidationKind kind) => kind switch
    {
        ValidationKind.Date => "validation.date",
        ValidationKind.Time => "validation.time",
        ValidationKind.TextLength => "validation.length",
        _ => "validation.value",
    };

    private string? Blank(string text) => text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
