using PulseOffice.Foundation.Text;
using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record DuplicateRemovalRequest(CellRange Range, bool HasHeader = true)
{
    public IReadOnlyList<int>? KeyColumns { get; init; }

    public bool CaseSensitive { get; init; }
}

public sealed record DuplicateRemovalPlan(IReadOnlyList<int> RowsToRemove, int Kept);

public interface IDuplicateRemover
{
    DuplicateRemovalPlan Plan(Worksheet sheet, DuplicateRemovalRequest request);
}

public sealed class DuplicateRemover(ISelectionWorkArea workArea, PulseOffice.Sheets.Formatting.ICellPresenter presenter, IDateEpoch epochs) : IDuplicateRemover
{
    private const double UnboundedWidth = 1000;

    public DuplicateRemover(ISelectionWorkArea workArea, PulseOffice.Sheets.Formatting.ICellPresenter presenter)
        : this(workArea, presenter, new AmbientDateEpoch())
    {
    }

    public DuplicateRemover(ISelectionWorkArea workArea)
        : this(workArea, new AmbientDateEpoch())
    {
    }

    private DuplicateRemover(ISelectionWorkArea workArea, IDateEpoch epochs)
        : this(workArea, new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(CultureInfo.CurrentCulture, new EpochDates(epochs).Date)), epochs)
    {
    }

    public DuplicateRemovalPlan Plan(Worksheet sheet, DuplicateRemovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        if (workArea.Bound(request.Range, sheet.UsedRange()) is not { } range) return new DuplicateRemovalPlan([], 0);
        using var epoch = epochs.Enter((sheet.Owner?.Calculation ?? CalculationSettings.Default).DateEpoch);
        var first = request.HasHeader ? range.Start.Row + 1 : range.Start.Row;

        var columns = request.KeyColumns
            ?? [.. Enumerable.Range(range.Start.Column, range.End.Column - range.Start.Column + 1)];

        var seen = new HashSet<string>(
            request.CaseSensitive ? StringComparer.Ordinal : StringComparer.CurrentCultureIgnoreCase);

        var duplicates = new List<int>();
        var kept = 0;

        for (var row = first; row <= range.End.Row; row++)
        {
            var key = string.Join('\u001f', columns.Select(column => Shown(sheet, new CellAddress(row, column))));

            if (seen.Add(key)) kept++;
            else duplicates.Add(row);
        }

        return new DuplicateRemovalPlan(duplicates, kept);
    }

    private string Shown(Worksheet sheet, CellAddress address)
    {
        if (sheet.Owner is not { } workbook || !sheet.TryGetCell(address, out var cell)) return Describe(sheet.GetValue(address));
        if (cell.Value.Kind is ValueKind.Empty) return string.Empty;
        if (cell.Value.Kind is ValueKind.Text) return cell.Value.AsText();
        return presenter.Present(cell, workbook.Styles, workbook.NumberFormats, UnboundedWidth).Text;
    }

    private string Describe(CellValue value) => (int)value.Kind + ":" + value.Kind switch
    {
        ValueKind.Empty => string.Empty,
        ValueKind.Text => value.AsText(),
        ValueKind.Boolean => value.AsBoolean() ? "1" : "0",
        ValueKind.Error => value.ErrorText,
        _ => value.AsNumber().ToString("R", CultureInfo.InvariantCulture),
    };
}

internal sealed class EpochDates(IDateEpoch epochs)
{
    public DateTime Date(double serial)
    {
        var calculation = new CalculationSettings { DateEpoch = epochs.Current };
        return calculation.TryDateOf(serial, out var moment) ? moment : calculation.DateEpoch.ToDateTime(TimeOnly.MinValue);
    }
}

public sealed record TextToColumnsRequest(CellRange Range)
{
    public IReadOnlyList<char> Separators { get; init; } = [';'];

    public char? Quote { get; init; } = '"';

    public bool MergeSeparators { get; init; }

    public bool DetectNumbers { get; init; } = true;
}

public sealed record TextToColumnsPlan(
    IReadOnlyDictionary<CellAddress, CellValue> Values, int WidestRow);

public interface ITextToColumns
{
    TextToColumnsPlan Plan(Worksheet sheet, TextToColumnsRequest request);

    IReadOnlyList<string> Split(string text, TextToColumnsRequest request);
}

public sealed class TextToColumns(IDelimitedText delimited, PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale, ISelectionWorkArea workArea) : ITextToColumns
{
    private readonly IDelimitedText _delimited = delimited;
    private readonly SeparatorUnifier _unifier = new();
    private readonly SplitNumberReader _numbers = new(new Evaluation.Functions.NumericText(locale));

    public TextToColumns(IDelimitedText delimited) : this(delimited, new PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale(), new SelectionWorkArea())
    {
    }

    public TextToColumnsPlan Plan(Worksheet sheet, TextToColumnsRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        var values = new Dictionary<CellAddress, CellValue>();
        var widest = 0;
        if (workArea.Bound(request.Range, sheet.UsedRange()) is not { } rows) return new TextToColumnsPlan(values, widest);

        for (var row = rows.Start.Row; row <= rows.End.Row; row++)
        {
            var source = sheet.GetValue(new CellAddress(row, request.Range.Start.Column));
            if (source.Kind != ValueKind.Text) continue;

            var parts = Split(source.AsText(), request);
            widest = Math.Max(widest, parts.Count);

            for (var index = 0; index < parts.Count; index++)
                values[new CellAddress(row, request.Range.Start.Column + index)] =
                    Interpret(parts[index], request.DetectNumbers);
        }

        return new TextToColumnsPlan(values, widest);
    }

    public IReadOnlyList<string> Split(string text, TextToColumnsRequest request)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Separators.Count == 0) return [text];

        var primary = request.Separators[0];

        return _delimited.SplitLine(_unifier.Unify(text, request.Separators, request.Quote), new DelimitedFormat
        {
            Separator = primary,
            Quote = request.Quote,
            MergeSeparators = request.MergeSeparators,
        });
    }

    private CellValue Interpret(string text, bool detectNumbers) =>
        text.Length == 0 ? CellValue.Empty
        : detectNumbers && _numbers.TryRead(text, out var number)
            ? CellValue.Number(number)
            : CellValue.Text(text);
}

internal sealed class SeparatorUnifier
{
    public string Unify(string text, IReadOnlyList<char> separators, char? quote)
    {
        if (separators.Count < 2) return text;

        var primary = separators[0];
        var builder = new System.Text.StringBuilder(text.Length);
        var quoted = false;

        foreach (var character in text)
        {
            if (quote is { } mark && character == mark) quoted = !quoted;

            builder.Append(!quoted && separators.Contains(character) ? primary : character);
        }

        return builder.ToString();
    }
}

internal sealed class SplitNumberReader(Evaluation.Functions.NumericText numbers)
{
    public bool TryRead(string text, out double number)
    {
        number = 0;

        var trimmed = text.Trim();
        if (trimmed.Length == 0 || HasLeadingZero(trimmed)) return false;

        return !trimmed.EndsWith('%') && numbers.TryRead(trimmed, out number);
    }

    private bool HasLeadingZero(string text)
    {
        var digits = text[0] is '-' or '+' ? text[1..] : text;
        return digits.Length > 1 && digits[0] == '0' && char.IsAsciiDigit(digits[1]);
    }
}
