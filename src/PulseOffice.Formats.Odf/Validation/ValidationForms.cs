using System.Text;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.Odf;

internal sealed class ValidationArguments
{
    public IReadOnlyList<string> Split(string text, char separator)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var quoted = false;

        foreach (var character in text)
        {
            if (character == '"') quoted = !quoted;
            if (!quoted && character is '(' or '[') depth++;
            if (!quoted && character is ')' or ']') depth--;

            if (!quoted && depth == 0 && character == separator)
            {
                pieces.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0 || pieces.Count > 0) pieces.Add(current.ToString().Trim());
        return pieces;
    }

    public string? Inside(string text, string function)
    {
        if (!text.StartsWith(function + "(", StringComparison.Ordinal)) return null;

        var start = function.Length + 1;
        var depth = 1;
        var quoted = false;

        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"') quoted = !quoted;
            if (quoted) continue;
            if (character == '(') depth++;
            if (character == ')' && --depth == 0) return text[start..index];
        }

        return null;
    }
}

internal sealed class ValidationFormulas(OdfFormulaSyntax syntax)
{
    public string Write(string? formula) =>
        string.IsNullOrWhiteSpace(formula) ? "0" : syntax.ToOdfExpression(formula);

    public string? Read(string? expression) =>
        string.IsNullOrWhiteSpace(expression) ? null : syntax.ToInternalExpression(expression.Trim());
}

internal abstract class ValidationComparisonForm
{
    public abstract bool Writes(ValidationOperator op);

    public abstract string Write(DataValidation validation, string subject);

    public abstract DataValidation? Read(string text, DataValidation validation, string subject);
}

internal sealed class RangeComparisonForm(ValidationOperator op, string suffix, ValidationArguments arguments, ValidationFormulas formulas)
    : ValidationComparisonForm
{
    public override bool Writes(ValidationOperator candidate) => candidate == op;

    public override string Write(DataValidation validation, string subject) =>
        $"{subject}-{suffix}({formulas.Write(validation.FirstFormula)},{formulas.Write(validation.SecondFormula)})";

    public override DataValidation? Read(string text, DataValidation validation, string subject)
    {
        if (arguments.Inside(text, $"{subject}-{suffix}") is not { } inside) return null;

        var parts = arguments.Split(inside, ',');
        return validation with
        {
            Operator = op,
            FirstFormula = formulas.Read(parts.ElementAtOrDefault(0)),
            SecondFormula = formulas.Read(parts.ElementAtOrDefault(1)),
        };
    }
}

internal sealed class SymbolComparisonForm(ValidationOperator op, string symbol, ValidationFormulas formulas) : ValidationComparisonForm
{
    public override bool Writes(ValidationOperator candidate) => candidate == op;

    public override string Write(DataValidation validation, string subject) =>
        $"{subject}(){symbol}{formulas.Write(validation.FirstFormula)}";

    public override DataValidation? Read(string text, DataValidation validation, string subject)
    {
        var content = subject + "()";
        if (!text.StartsWith(content, StringComparison.Ordinal)) return null;

        var expression = text[content.Length..].Trim();
        return expression.StartsWith(symbol, StringComparison.Ordinal)
            ? validation with { Operator = op, FirstFormula = formulas.Read(expression[symbol.Length..]) }
            : null;
    }
}

internal sealed class ValidationComparisons(ValidationArguments arguments, ValidationFormulas formulas)
{
    private readonly IReadOnlyList<ValidationComparisonForm> _forms =
    [
        new RangeComparisonForm(ValidationOperator.Between, "is-between", arguments, formulas),
        new RangeComparisonForm(ValidationOperator.NotBetween, "is-not-between", arguments, formulas),
        new SymbolComparisonForm(ValidationOperator.GreaterOrEqual, ">=", formulas),
        new SymbolComparisonForm(ValidationOperator.LessOrEqual, "<=", formulas),
        new SymbolComparisonForm(ValidationOperator.NotEqual, "!=", formulas),
        new SymbolComparisonForm(ValidationOperator.Greater, ">", formulas),
        new SymbolComparisonForm(ValidationOperator.Less, "<", formulas),
        new SymbolComparisonForm(ValidationOperator.Equal, "=", formulas),
    ];

    public string Write(DataValidation validation, string subject) =>
        (_forms.FirstOrDefault(form => form.Writes(validation.Operator)) ?? _forms[3]).Write(validation, subject);

    public DataValidation? Read(string text, DataValidation validation, string subject) =>
        _forms.Select(form => form.Read(text, validation, subject)).FirstOrDefault(read => read is not null);
}

internal abstract class ValidationKindForm
{
    public abstract ValidationKind Kind { get; }

    public abstract string Write(DataValidation validation);

    public abstract DataValidation? Read(string condition, DataValidation validation);
}

internal sealed class ListKindForm(ValidationArguments arguments, ValidationFormulas formulas) : ValidationKindForm
{
    private const string Function = "cell-content-is-in-list";

    public override ValidationKind Kind => ValidationKind.List;

    public override string Write(DataValidation validation)
    {
        var inside = validation.ListValues.Count == 0 && !string.IsNullOrWhiteSpace(validation.FirstFormula)
            ? formulas.Write(validation.FirstFormula)
            : string.Join(';', validation.ListValues.Select(value => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""));
        return $"of:{Function}({inside})";
    }

    public override DataValidation? Read(string condition, DataValidation validation)
    {
        if (arguments.Inside(condition, Function) is not { } inside) return null;

        var items = arguments.Split(inside, ';');
        if (items.Count > 0 && items.All(IsTextLiteral))
            return validation with
            {
                Kind = ValidationKind.List,
                ListValues = [.. items.Select(item => item[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal))],
            };

        return validation with { Kind = ValidationKind.List, FirstFormula = formulas.Read(inside) };
    }

    private bool IsTextLiteral(string item) =>
        item.Length >= 2 && item[0] == '"' && item[^1] == '"'
        && !item[1..^1].Replace("\"\"", string.Empty, StringComparison.Ordinal).Contains('"');
}

internal sealed class ComparedKindForm(ValidationKind kind, string function, ValidationComparisons comparisons) : ValidationKindForm
{
    private const string Subject = "cell-content";

    public override ValidationKind Kind => kind;

    public override string Write(DataValidation validation) => $"of:{function}() and {comparisons.Write(validation, Subject)}";

    public override DataValidation? Read(string condition, DataValidation validation)
    {
        if (!condition.StartsWith(function + "()", StringComparison.Ordinal)) return null;

        var typed = validation with { Kind = kind };
        var rest = condition[(function.Length + 2)..].Trim();
        if (!rest.StartsWith("and", StringComparison.Ordinal)) return typed;
        return comparisons.Read(rest[3..].Trim(), typed, Subject) ?? typed;
    }
}

internal sealed class TextLengthKindForm(ValidationComparisons comparisons) : ValidationKindForm
{
    private const string Subject = "cell-content-text-length";

    public override ValidationKind Kind => ValidationKind.TextLength;

    public override string Write(DataValidation validation) => "of:" + comparisons.Write(validation, Subject);

    public override DataValidation? Read(string condition, DataValidation validation)
    {
        var typed = validation with { Kind = ValidationKind.TextLength };
        if (!condition.StartsWith(Subject, StringComparison.Ordinal)) return null;
        if (condition.StartsWith(Subject + "() and ", StringComparison.Ordinal))
            return comparisons.Read(condition[(Subject.Length + 7)..].Trim(), typed, "cell-content") ?? typed;
        return comparisons.Read(condition, typed, Subject) ?? typed;
    }
}

internal sealed class CustomKindForm(ValidationArguments arguments, ValidationFormulas formulas) : ValidationKindForm
{
    private const string Function = "is-true-formula";

    public override ValidationKind Kind => ValidationKind.Custom;

    public override string Write(DataValidation validation) =>
        $"of:{Function}({(string.IsNullOrWhiteSpace(validation.FirstFormula) ? "TRUE()" : formulas.Write(validation.FirstFormula))})";

    public override DataValidation? Read(string condition, DataValidation validation) =>
        arguments.Inside(condition, Function) is { } inside
            ? validation with { Kind = ValidationKind.Custom, FirstFormula = formulas.Read(inside) }
            : null;
}

internal sealed class OdfValidationSyntax
{
    private readonly IReadOnlyList<ValidationKindForm> _forms;
    private readonly ValidationFormulas _formulas;

    public OdfValidationSyntax()
    {
        var arguments = new ValidationArguments();
        _formulas = new ValidationFormulas(new OdfFormulaSyntax());
        var comparisons = new ValidationComparisons(arguments, _formulas);
        _forms =
        [
            new ListKindForm(arguments, _formulas),
            new ComparedKindForm(ValidationKind.WholeNumber, "cell-content-is-whole-number", comparisons),
            new ComparedKindForm(ValidationKind.Decimal, "cell-content-is-decimal-number", comparisons),
            new ComparedKindForm(ValidationKind.Date, "cell-content-is-date", comparisons),
            new ComparedKindForm(ValidationKind.Time, "cell-content-is-time", comparisons),
            new TextLengthKindForm(comparisons),
            new CustomKindForm(arguments, _formulas),
        ];
    }

    public string? Write(DataValidation validation) =>
        _forms.FirstOrDefault(form => form.Kind == validation.Kind)?.Write(validation);

    public DataValidation Read(string? condition, DataValidation validation)
    {
        if (string.IsNullOrWhiteSpace(condition)) return validation with { Kind = ValidationKind.Any };

        var text = condition.Trim();
        var colon = text.IndexOf(':');
        var bare = colon > 0 && colon < 6 && !text[..colon].Contains('(') ? text[(colon + 1)..] : text;

        return _forms.Select(form => form.Read(bare, validation)).FirstOrDefault(read => read is not null)
            ?? validation with { Kind = ValidationKind.Custom, FirstFormula = _formulas.Read(bare) };
    }
}
