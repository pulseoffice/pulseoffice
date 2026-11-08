using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using Sheet = PulseOffice.Sheets.Worksheet;
using Book = PulseOffice.Sheets.Workbook;
using Rule = PulseOffice.Sheets.DataValidation;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxMetadataMapper
{
    private readonly ValidationVocabulary _vocabulary = new();
    private readonly XlsxValidationExtension _validationExtension = new(new X14Namespaces());

    public DataValidations? BuildValidations(Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.Validations.Count == 0) return null;

        var container = new DataValidations();

        foreach (var validation in sheet.Validations)
        {
            var element = new DocumentFormat.OpenXml.Spreadsheet.DataValidation
            {
                Type = _vocabulary.Kinds.Sdk(validation.Kind),
                AllowBlank = validation.AllowBlank,
                ShowInputMessage = validation.ShowInputMessage && (validation.InputMessage is { Length: > 0 } || validation.InputTitle is { Length: > 0 }),
                ShowErrorMessage = validation.Kind != ValidationKind.Any && validation.ShowErrorAlert,
                SequenceOfReferences = new ListValue<StringValue> { InnerText = Reference(validation.Range) },
                ErrorStyle = _vocabulary.Alerts.Sdk(validation.Alert),
            };

            if (validation.Kind != ValidationKind.List)
                element.Operator = _vocabulary.Operators.Sdk(validation.Operator);

            if (validation.ErrorMessage is { Length: > 0 } message) element.Error = message;
            if (validation.ErrorTitle is { Length: > 0 } title) element.ErrorTitle = title;
            if (validation.InputMessage is { Length: > 0 } prompt) element.Prompt = prompt;
            if (validation.InputTitle is { Length: > 0 } promptTitle) element.PromptTitle = promptTitle;

            if (validation.Kind == ValidationKind.List)
            {
                if (!validation.ShowDropdown) element.ShowDropDown = true;
                element.AppendChild(new Formula1(validation.ListValues.Count > 0
                    ? "\"" + string.Join(',', validation.ListValues).Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
                    : ToOpenXml(validation.FirstFormula)));
            }
            else
            {
                if (validation.FirstFormula is { Length: > 0 } first)
                    element.AppendChild(new Formula1(ToOpenXml(first)));

                if (validation.SecondFormula is { Length: > 0 } second)
                    element.AppendChild(new Formula2(ToOpenXml(second)));
            }

            container.AppendChild(element);
        }

        container.Count = (uint)sheet.Validations.Count;
        return container;
    }

    public void ReadValidations(DocumentFormat.OpenXml.Spreadsheet.Worksheet worksheet, Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(sheet);

        var elements = worksheet.Elements<DataValidations>().SelectMany(container => container.Elements<DocumentFormat.OpenXml.Spreadsheet.DataValidation>())
            .Concat(_validationExtension.Read(worksheet.GetFirstChild<WorksheetExtensionList>()));
        foreach (var element in elements)
        {
            var references = element.SequenceOfReferences?.InnerText ?? string.Empty;

            foreach (var part in references.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!CellRange.TryParseA1(part.Replace("$", string.Empty, StringComparison.Ordinal), out var range))
                    continue;

                var kind = _vocabulary.Kinds.Model(element.Type?.Value);
                var first = element.GetFirstChild<Formula1>()?.Text;
                var second = element.GetFirstChild<Formula2>()?.Text;

                sheet.Validations.Add(new Rule
                {
                    Range = range,
                    Kind = kind,
                    Operator = _vocabulary.Operators.Model(element.Operator?.Value),
                    AllowBlank = element.AllowBlank?.Value ?? true,
                    FirstFormula = kind == ValidationKind.List && IsLiteralList(first) ? null : ToInternal(first),
                    SecondFormula = kind is ValidationKind.List or ValidationKind.Custom
                        || element.Operator?.Value is { } op && op != DataValidationOperatorValues.Between && op != DataValidationOperatorValues.NotBetween
                        ? null
                        : ToInternal(second),
                    ListValues = kind == ValidationKind.List && IsLiteralList(first) ? Choices(first) : [],
                    ShowDropdown = element.ShowDropDown?.Value != true,
                    Alert = _vocabulary.Alerts.Model(element.ErrorStyle?.Value),
                    ErrorMessage = element.Error?.Value,
                    ErrorTitle = element.ErrorTitle?.Value,
                    InputMessage = element.Prompt?.Value,
                    InputTitle = element.PromptTitle?.Value,
                    ShowInputMessage = element.ShowInputMessage?.Value == true || (element.Prompt?.Value is not { Length: > 0 } && element.PromptTitle?.Value is not { Length: > 0 }),
                    ShowErrorAlert = element.ShowErrorMessage?.Value == true || kind == ValidationKind.Any,
                });
            }
        }
    }

    public IEnumerable<DocumentFormat.OpenXml.Spreadsheet.DefinedName> BuildDefinedNames(Book workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            var sheet = workbook.Sheets[index];

            if (sheet.PrintRanges.Count > 0)
                yield return new DocumentFormat.OpenXml.Spreadsheet.DefinedName
                {
                    Name = "_xlnm.Print_Area",
                    LocalSheetId = (uint)index,
                    Text = string.Join(',', sheet.PrintRanges.Select(range => Absolute(sheet.Name, range))),
                };

            var titles = new List<string>();
            if (sheet.RepeatedPrintColumns > 0)
                titles.Add($"{Quoted(sheet.Name)}!$A:${CellAddress.ColumnName(sheet.RepeatedPrintColumns - 1)}");
            if (sheet.RepeatedPrintRows > 0)
                titles.Add(string.Create(CultureInfo.InvariantCulture, $"{Quoted(sheet.Name)}!$1:${sheet.RepeatedPrintRows}"));
            if (titles.Count > 0)
                yield return new DocumentFormat.OpenXml.Spreadsheet.DefinedName
                {
                    Name = "_xlnm.Print_Titles",
                    LocalSheetId = (uint)index,
                    Text = string.Join(',', titles),
                };
        }

        foreach (var name in workbook.DefinedNames)
        {
            var scope = name.Scope is { } scoped ? workbook.Sheets.ToList().FindIndex(sheet => string.Equals(sheet.Name, scoped, StringComparison.OrdinalIgnoreCase)) : -1;
            var defined = new DocumentFormat.OpenXml.Spreadsheet.DefinedName
            {
                Name = name.Name,
                Text = _formulas.ToOpenXml("=" + Qualified(scope >= 0 ? name.Scope! : workbook.Sheets.FirstOrDefault()?.Name, name.Expression.TrimStart('='))).TrimStart('='),
            };
            if (scope >= 0) defined.LocalSheetId = (uint)scope;
            yield return defined;
        }
    }

    private readonly XlsxFormulaSyntax _formulas = new();

    private readonly XlsxQueryTableNames _queryNames = new();

    private readonly XlsxRangeFilters _rangeFilters = new();

    private string Quoted(string sheetName) => $"'{sheetName.Replace("'", "''", StringComparison.Ordinal)}'";

    private string Qualified(string? sheetName, string expression)
    {
        var isBareRange = CellRange.TryParseA1(expression.Replace("$", string.Empty, StringComparison.Ordinal), out _);
        return isBareRange && sheetName is not null ? $"{Quoted(sheetName)}.{expression}" : expression;
    }

    public void ReadDefinedNames(WorkbookPart part, Book workbook)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(workbook);

        var names = part.Workbook?.GetFirstChild<DefinedNames>();
        if (names is null) return;

        _rangeFilters.Read(names.Elements<DocumentFormat.OpenXml.Spreadsheet.DefinedName>(), workbook);

        foreach (var name in names.Elements<DocumentFormat.OpenXml.Spreadsheet.DefinedName>())
        {
            if (_queryNames.Claims(part, name.LocalSheetId?.Value, name.Name?.Value)) continue;
            if (name.Name?.Value is { Length: > 0 } user && !user.StartsWith("_xlnm.", StringComparison.Ordinal))
            {
                var scope = name.LocalSheetId?.Value is { } local && local < workbook.Sheets.Count ? workbook.Sheets[(int)local].Name : null;
                if (_formulas.ToInternal(name.Text) is { } expression)
                    workbook.DefineName(new PulseOffice.Sheets.DefinedName(user, expression.TrimStart('='), scope));
                continue;
            }

            if (name.LocalSheetId?.Value is not { } index || index >= workbook.Sheets.Count) continue;

            var sheet = workbook.Sheets[(int)index];

            if (name.Name == "_xlnm.Print_Area")
                foreach (var piece in name.Text.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (CellRange.TryParseA1(Strip(piece), out var range))
                        sheet.PrintRanges.Add(range);

            if (name.Name != "_xlnm.Print_Titles") continue;

            foreach (var piece in name.Text.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var bounds = Strip(piece).Split(':');
                if (bounds.Length != 2) continue;
                if (int.TryParse(bounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var last))
                    sheet.RepeatedPrintRows = last;
                else if (CellAddress.TryParseA1(bounds[1] + "1", out var column))
                    sheet.RepeatedPrintColumns = column.Column + 1;
            }
        }
    }

    private string Strip(string reference)
    {
        var bang = reference.LastIndexOf('!');
        return (bang >= 0 ? reference[(bang + 1)..] : reference).Replace("$", string.Empty, StringComparison.Ordinal);
    }

    private string Absolute(string sheetName, CellRange range) =>
        $"{Quoted(sheetName)}!${CellAddress.ColumnName(range.Start.Column)}${range.Start.Row + 1}" +
        $":${CellAddress.ColumnName(range.End.Column)}${range.End.Row + 1}";

    private string Reference(CellRange range) =>
        range.IsSingleCell ? range.Start.ToA1() : $"{range.Start.ToA1()}:{range.End.ToA1()}";

    private bool IsLiteralList(string? formula) =>
        formula?.Trim() is { Length: >= 2 } text && text[0] == '"' && text[^1] == '"';

    private IReadOnlyList<string> Choices(string? formula)
    {
        if (formula is null) return [];

        var text = formula.Trim()[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        return [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private string ToOpenXml(string? formula) =>
        string.IsNullOrWhiteSpace(formula) ? string.Empty : _formulas.ToOpenXml("=" + formula.TrimStart('=')).TrimStart('=');

    private string? ToInternal(string? formula) =>
        string.IsNullOrWhiteSpace(formula) ? null : _formulas.ToInternal(formula).TrimStart('=');
}

internal sealed class ValidationVocabulary
{
    public OoxmlValueMap<ValidationKind, DataValidationValues> Kinds { get; } = new(ValidationKind.Any, DataValidationValues.None,
        (ValidationKind.WholeNumber, DataValidationValues.Whole), (ValidationKind.Decimal, DataValidationValues.Decimal),
        (ValidationKind.List, DataValidationValues.List), (ValidationKind.Date, DataValidationValues.Date),
        (ValidationKind.Time, DataValidationValues.Time), (ValidationKind.TextLength, DataValidationValues.TextLength),
        (ValidationKind.Custom, DataValidationValues.Custom));

    public OoxmlValueMap<ValidationOperator, DataValidationOperatorValues> Operators { get; } = new(ValidationOperator.Between, DataValidationOperatorValues.Between,
        (ValidationOperator.NotBetween, DataValidationOperatorValues.NotBetween), (ValidationOperator.Equal, DataValidationOperatorValues.Equal),
        (ValidationOperator.NotEqual, DataValidationOperatorValues.NotEqual), (ValidationOperator.Greater, DataValidationOperatorValues.GreaterThan),
        (ValidationOperator.Less, DataValidationOperatorValues.LessThan), (ValidationOperator.GreaterOrEqual, DataValidationOperatorValues.GreaterThanOrEqual),
        (ValidationOperator.LessOrEqual, DataValidationOperatorValues.LessThanOrEqual));

    public OoxmlValueMap<ValidationAlert, DataValidationErrorStyleValues> Alerts { get; } = new(ValidationAlert.Stop, DataValidationErrorStyleValues.Stop,
        (ValidationAlert.Warning, DataValidationErrorStyleValues.Warning), (ValidationAlert.Information, DataValidationErrorStyleValues.Information));
}
