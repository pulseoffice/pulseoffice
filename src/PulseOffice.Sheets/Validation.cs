using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum ValidationKind
{
    Any, WholeNumber, Decimal, List, Date, Time, TextLength, Custom,
}

public enum ValidationOperator
{
    Between, NotBetween, Equal, NotEqual, Greater, Less, GreaterOrEqual, LessOrEqual,
}

public enum ValidationAlert { Stop, Warning, Information }

public sealed record DataValidation
{
    public required CellRange Range { get; init; }

    public ValidationKind Kind { get; init; } = ValidationKind.Any;

    public ValidationOperator Operator { get; init; } = ValidationOperator.Between;

    public string? FirstFormula { get; init; }

    public string? SecondFormula { get; init; }

    public IReadOnlyList<string> ListValues { get; init; } = [];

    public bool AllowBlank { get; init; } = true;

    public bool ShowDropdown { get; init; } = true;

    public string? InputTitle { get; init; }

    public string? InputMessage { get; init; }

    public bool ShowInputMessage { get; init; } = true;

    public bool ShowErrorAlert { get; init; } = true;

    public ValidationAlert Alert { get; init; } = ValidationAlert.Stop;

    public string? ErrorTitle { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record SheetProtection
{
    public bool IsEnabled { get; init; }

    public string? PasswordHash { get; init; }

    public bool AllowSelectLockedCells { get; init; } = true;

    public bool AllowSelectUnlockedCells { get; init; } = true;

    public bool AllowFormatCells { get; init; }

    public bool AllowInsertRows { get; init; }

    public bool AllowInsertColumns { get; init; }

    public bool AllowDeleteRows { get; init; }

    public bool AllowDeleteColumns { get; init; }

    public bool AllowSort { get; init; }

    public bool AllowFilter { get; init; }

    public bool Accepts(string? password) =>
        PasswordHash is null || (password is not null && PasswordKey.Parse(PasswordHash).Matches(password));

    public static readonly SheetProtection None = new();
}
