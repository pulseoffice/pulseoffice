using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

[Flags]
public enum PasteParts
{
    Values = 1 << 0,
    Formulas = 1 << 1,
    Styles = 1 << 2,

    Comments = 1 << 3,

    Validation = 1 << 4,

    All = Values | Formulas | Styles,

    Everything = All | Comments | Validation,

    ValuesOnly = Values,

    StylesOnly = Styles,
}

public enum PasteOperation { None, Add, Subtract, Multiply, Divide }

public sealed record PasteRequest
{
    public PasteParts Parts { get; init; } = PasteParts.All;

    public bool Transpose { get; init; }

    public bool SkipBlanks { get; init; }

    public PasteOperation Operation { get; init; } = PasteOperation.None;

    public bool Link { get; init; }

    public string HistoryLabel { get; init; } = "Paste";

    public static readonly PasteRequest All = new();
}

public sealed record ClipboardSnapshot(
    int RowCount, int ColumnCount, IReadOnlyList<(int Row, int Column, Cell Cell)> Cells)
{
    public bool IsEmpty => Cells.Count == 0 && Comments.Count == 0 && Validations.Count == 0;

    public IReadOnlyList<(int Row, int Column, CellComment Comment)> Comments { get; init; } = [];

    public IReadOnlyList<CopiedValidation> Validations { get; init; } = [];

    public IReadOnlyList<CellRange> Merges { get; init; } = [];

    public string? SourceSheet { get; init; }
}

public sealed record CopiedValidation(int Row, int Column, int RowCount, int ColumnCount, DataValidation Rule);

public interface ISheetClipboard
{
    ClipboardSnapshot Copy(Worksheet sheet, CellRange range);

    void Paste(ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target, CellAddress origin, PasteRequest request, UndoStack? history = null);

    string ToDelimitedText(ClipboardSnapshot snapshot);

    ClipboardSnapshot FromDelimitedText(string text);

    ClipboardSnapshot FromGrid(IReadOnlyList<IReadOnlyList<string?>> rows);
}

public sealed class SheetClipboard(PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale) : ISheetClipboard
{
    public SheetClipboard() : this(new PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale())
    {
    }

    private readonly FormulaTranslator _translator = new();

    private readonly IReadOnlyDictionary<PasteOperation, PasteArithmetic> _operations =
        new PasteArithmetic[] { new KeepSource(), new AddValues(), new SubtractValues(), new MultiplyValues(), new DivideValues() }
            .ToDictionary(operation => operation.Operation);

    private readonly ClipboardExtent _extent = new();

    private readonly TabularText _text = new(locale);

    private readonly Parsing.SheetNameQuoting _quoting = new();

    public ClipboardSnapshot Copy(Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var cells = new List<(int, int, Cell)>();

        foreach (var address in Occupied(sheet, range).Cells())
        {
            var own = sheet[address];
            var styleId = sheet.StyleIdAt(address);
            var cell = own with { StyleId = styleId, ExplicitStyle = true };

            cells.Add((address.Row - range.Start.Row, address.Column - range.Start.Column, cell));
        }

        var comments = sheet.Comments
            .Where(pair => range.Contains(pair.Key))
            .Select(pair => (pair.Key.Row - range.Start.Row, pair.Key.Column - range.Start.Column, pair.Value))
            .ToList();

        var validations = sheet.Validations
            .Where(rule => rule.Range.IntersectsWith(range))
            .Select(rule => Clipped(rule, range))
            .ToList();

        var merges = sheet.Merges.All
            .Where(region => range.Contains(region.Start) && range.Contains(region.End))
            .Select(region => new CellRange(
                new CellAddress(region.Start.Row - range.Start.Row, region.Start.Column - range.Start.Column),
                new CellAddress(region.End.Row - range.Start.Row, region.End.Column - range.Start.Column)))
            .ToList();

        return new ClipboardSnapshot(range.RowCount, range.ColumnCount, cells)
        {
            Comments = comments,
            Validations = validations,
            Merges = merges,
            SourceSheet = sheet.Name,
        };
    }

    private CellRange Occupied(Worksheet sheet, CellRange range)
    {
        var bottom = Math.Min(range.End.Row, Math.Max(sheet.LastFormattedRow, range.Start.Row));
        var right = Math.Min(range.End.Column, Math.Max(sheet.LastFormattedColumn, range.Start.Column));
        return new CellRange(range.Start, new CellAddress(bottom, right));
    }

    private CopiedValidation Clipped(DataValidation rule, CellRange range)
    {
        var top = Math.Max(rule.Range.Start.Row, range.Start.Row);
        var left = Math.Max(rule.Range.Start.Column, range.Start.Column);
        var bottom = Math.Min(rule.Range.End.Row, range.End.Row);
        var right = Math.Min(rule.Range.End.Column, range.End.Column);
        return new CopiedValidation(top - range.Start.Row, left - range.Start.Column, bottom - top + 1, right - left + 1, rule);
    }

    public void Paste(
        ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target,
        CellAddress origin, PasteParts parts = PasteParts.All, UndoStack? history = null) =>
        Paste(snapshot, sheet, target, origin, new PasteRequest { Parts = parts }, history);

    public void Paste(
        ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target,
        CellAddress origin, PasteRequest request, UndoStack? history = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        if (snapshot.IsEmpty && !request.Link) return;

        if (request.Link && snapshot.SourceSheet is { } sourceSheet)
        {
            PasteLinks(snapshot, sheet, target, origin, sourceSheet, request, history);
            return;
        }

        var rowDelta = target.Row - origin.Row;
        var columnDelta = target.Column - origin.Column;
        var arithmetic = _operations[request.Operation];

        using var transaction = history?.BeginTransaction(request.HistoryLabel);

        var visited = new HashSet<CellAddress>();

        foreach (var (row, column, source) in snapshot.Cells)
            PasteCell(sheet, target, origin, request, arithmetic, row, column, source, rowDelta, columnDelta, visited, history);

        if (!request.SkipBlanks)
            foreach (var (row, column) in UncopiedOccupants(snapshot, sheet, target, request.Transpose, visited))
                PasteCell(sheet, target, origin, request, arithmetic, row, column, _uncopiedBlank, rowDelta, columnDelta, visited, history);

        if (request.Parts.HasFlag(PasteParts.Styles)) PasteMerges(snapshot, sheet, target, request.Transpose, request.HistoryLabel, history);
        if (request.Parts.HasFlag(PasteParts.Comments)) PasteComments(snapshot, sheet, target, request.Transpose, request.HistoryLabel, history);
        if (request.Parts.HasFlag(PasteParts.Validation)) PasteValidations(snapshot, sheet, target, request.Transpose, request.HistoryLabel, history);
    }

    private readonly Cell _uncopiedBlank = Cell.Blank with { ExplicitStyle = true };

    private void PasteCell(
        Worksheet sheet, CellAddress target, CellAddress origin, PasteRequest request, PasteArithmetic arithmetic,
        int row, int column, Cell source, int rowDelta, int columnDelta, HashSet<CellAddress> visited, UndoStack? history)
    {
        var destination = request.Transpose
            ? new CellAddress(target.Row + column, target.Column + row)
            : new CellAddress(target.Row + row, target.Column + column);

        if (!destination.IsValid) return;
        visited.Add(destination);
        if (request.SkipBlanks && source.Value.IsEmpty && !source.HasFormula) return;

        var previous = sheet[destination];

        var next = request.Transpose
            ? Merge(previous, source, request.Parts,
                destination.Row - (origin.Row + row), destination.Column - (origin.Column + column))
            : Merge(previous, source, request.Parts, rowDelta, columnDelta);

        if (request.Operation != PasteOperation.None
            && (request.Parts.HasFlag(PasteParts.Values) || request.Parts.HasFlag(PasteParts.Formulas)))
            next = next with { Value = arithmetic.Combine(previous.Value, source.Value), Formula = null };

        if (previous == next) return;

        sheet[destination] = next;

        history?.Push(new DelegateAction(
            request.HistoryLabel,
            redo: () => sheet[destination] = next,
            undo: () => sheet[destination] = previous));
    }

    private List<(int Row, int Column)> UncopiedOccupants(
        ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target, bool transpose, HashSet<CellAddress> visited)
    {
        var area = transpose
            ? new CellRange(target, Bounded(target.Row + snapshot.ColumnCount - 1, target.Column + snapshot.RowCount - 1))
            : new CellRange(target, Bounded(target.Row + snapshot.RowCount - 1, target.Column + snapshot.ColumnCount - 1));

        return [.. sheet.NonEmptyCellsIn(area)
            .Select(pair => pair.Key)
            .Where(address => !visited.Contains(address))
            .Select(address => transpose
                ? (address.Column - target.Column, address.Row - target.Row)
                : (address.Row - target.Row, address.Column - target.Column))];
    }

    private void PasteLinks(
        ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target, CellAddress origin, string source, PasteRequest request, UndoStack? history)
    {
        var transpose = request.Transpose;
        using var transaction = history?.BeginTransaction(request.HistoryLabel);
        var values = snapshot.Cells.ToDictionary(entry => (entry.Row, entry.Column), entry => entry.Cell.Value);
        var sameSheet = string.Equals(source, sheet.Name, StringComparison.Ordinal);

        var rows = _extent.Rows(snapshot);
        var columns = _extent.Columns(snapshot);

        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var destination = transpose
                    ? new CellAddress(target.Row + column, target.Column + row)
                    : new CellAddress(target.Row + row, target.Column + column);
                var linked = new CellAddress(origin.Row + row, origin.Column + column);
                if (!destination.IsValid || !linked.IsValid) continue;

                var reference = sameSheet ? linked.ToA1() : _quoting.Qualified(source, linked.ToA1());
                var previous = sheet[destination];
                var next = previous with { Formula = "=" + reference, Value = values.GetValueOrDefault((row, column), CellValue.Number(0)) };
                if (previous == next) continue;

                sheet[destination] = next;
                var address = destination;
                history?.Push(new DelegateAction(request.HistoryLabel, redo: () => sheet[address] = next, undo: () => sheet[address] = previous));
            }
    }

    private void PasteMerges(ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target, bool transpose, string label, UndoStack? history)
    {
        foreach (var merge in snapshot.Merges)
        {
            var start = Placed(target, merge.Start, transpose);
            var end = Placed(target, merge.End, transpose);
            if (!start.IsValid || !end.IsValid) continue;

            var region = new CellRange(start, end);
            var replaced = sheet.Merges.RemoveOverlapping(region);
            sheet.Merges.Add(region);
            history?.Push(new DelegateAction(
                label,
                redo: () =>
                {
                    sheet.Merges.RemoveOverlapping(region);
                    sheet.Merges.Add(region);
                },
                undo: () =>
                {
                    sheet.Merges.Remove(region);
                    sheet.Merges.AddRange(replaced);
                }));
        }
    }

    private CellAddress Placed(CellAddress target, CellAddress offset, bool transpose) => transpose
        ? new CellAddress(target.Row + offset.Column, target.Column + offset.Row)
        : new CellAddress(target.Row + offset.Row, target.Column + offset.Column);

    private void PasteComments(ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target, bool transpose, string label, UndoStack? history)
    {
        foreach (var (row, column, comment) in snapshot.Comments)
        {
            var destination = transpose
                ? new CellAddress(target.Row + column, target.Column + row)
                : new CellAddress(target.Row + row, target.Column + column);
            if (!destination.IsValid) continue;

            var had = sheet.Comments.TryGetValue(destination, out var previous);
            sheet.Comments[destination] = comment;
            var address = destination;
            history?.Push(new DelegateAction(
                label,
                redo: () => sheet.Comments[address] = comment,
                undo: () =>
                {
                    if (had) sheet.Comments[address] = previous!;
                    else sheet.Comments.Remove(address);
                }));
        }
    }

    private void PasteValidations(ClipboardSnapshot snapshot, Worksheet sheet, CellAddress target, bool transpose, string label, UndoStack? history)
    {
        if (snapshot.Validations.Count == 0) return;

        List<DataValidation> before = [.. sheet.Validations];
        var area = transpose
            ? new CellRange(target, Bounded(target.Row + snapshot.ColumnCount - 1, target.Column + snapshot.RowCount - 1))
            : new CellRange(target, Bounded(target.Row + snapshot.RowCount - 1, target.Column + snapshot.ColumnCount - 1));

        sheet.Validations.RemoveAll(rule => area.Contains(rule.Range.Start) && area.Contains(rule.Range.End));

        foreach (var copied in snapshot.Validations)
        {
            var start = transpose
                ? Bounded(target.Row + copied.Column, target.Column + copied.Row)
                : Bounded(target.Row + copied.Row, target.Column + copied.Column);
            var end = transpose
                ? Bounded(start.Row + copied.ColumnCount - 1, start.Column + copied.RowCount - 1)
                : Bounded(start.Row + copied.RowCount - 1, start.Column + copied.ColumnCount - 1);
            sheet.Validations.Add(copied.Rule with { Range = new CellRange(start, end) });
        }

        List<DataValidation> after = [.. sheet.Validations];
        history?.Push(new DelegateAction(
            label,
            redo: () => { sheet.Validations.Clear(); sheet.Validations.AddRange(after); },
            undo: () => { sheet.Validations.Clear(); sheet.Validations.AddRange(before); }));
    }

    private CellAddress Bounded(int row, int column) =>
        new(Math.Min(row, CellAddress.MaxRows - 1), Math.Min(column, CellAddress.MaxColumns - 1));

    public string ToDelimitedText(ClipboardSnapshot snapshot) => _text.Write(snapshot);

    public ClipboardSnapshot FromDelimitedText(string text) => _text.Read(text);

    public ClipboardSnapshot FromGrid(IReadOnlyList<IReadOnlyList<string?>> rows) => _text.FromGrid(rows);

    private Cell Merge(Cell previous, Cell source, PasteParts parts, int rowDelta, int columnDelta)
    {
        var result = previous;

        if (parts.HasFlag(PasteParts.Styles)) result = result with { StyleId = source.StyleId, ExplicitStyle = source.HasStyle };

        if (parts.HasFlag(PasteParts.Formulas) && source.HasFormula)
        {
            var translated = _translator.Translate(source.Formula!, rowDelta, columnDelta) ?? source.Formula!;
            return result with { Formula = translated, Value = source.Value };
        }

        return parts.HasFlag(PasteParts.Values) ? result with { Value = source.Value, Formula = null } : result;
    }
}

internal abstract class PasteArithmetic
{
    public abstract PasteOperation Operation { get; }

    public CellValue Combine(CellValue previous, CellValue source) =>
        Operation != PasteOperation.None && previous.Kind == ValueKind.Number && source.Kind == ValueKind.Number
            ? Apply(previous.AsNumber(), source.AsNumber())
            : source;

    protected abstract CellValue Apply(double left, double right);
}

internal sealed class KeepSource : PasteArithmetic
{
    public override PasteOperation Operation => PasteOperation.None;
    protected override CellValue Apply(double left, double right) => CellValue.Number(right);
}

internal sealed class AddValues : PasteArithmetic
{
    public override PasteOperation Operation => PasteOperation.Add;
    protected override CellValue Apply(double left, double right) => CellValue.Number(left + right);
}

internal sealed class SubtractValues : PasteArithmetic
{
    public override PasteOperation Operation => PasteOperation.Subtract;
    protected override CellValue Apply(double left, double right) => CellValue.Number(left - right);
}

internal sealed class MultiplyValues : PasteArithmetic
{
    public override PasteOperation Operation => PasteOperation.Multiply;
    protected override CellValue Apply(double left, double right) => CellValue.Number(left * right);
}

internal sealed class DivideValues : PasteArithmetic
{
    public override PasteOperation Operation => PasteOperation.Divide;
    protected override CellValue Apply(double left, double right) =>
        right == 0 ? CellValue.FromError(CellError.DivideByZero) : CellValue.Number(left / right);
}

internal sealed class ClipboardExtent
{
    public int Rows(ClipboardSnapshot snapshot) =>
        Span(snapshot.RowCount, CellAddress.MaxRows, snapshot.Cells.Select(entry => entry.Row));

    public int Columns(ClipboardSnapshot snapshot) =>
        Span(snapshot.ColumnCount, CellAddress.MaxColumns, snapshot.Cells.Select(entry => entry.Column));

    private int Span(int copied, int whole, IEnumerable<int> occupied) =>
        copied < whole ? copied : occupied.DefaultIfEmpty(-1).Max() + 1;
}

internal sealed class TabularText(PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale)
{
    private readonly ClipboardExtent _extent = new();

    public string Write(ClipboardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var rows = _extent.Rows(snapshot);
        var columns = _extent.Columns(snapshot);
        var grid = new string[rows, columns];
        foreach (var (row, column, cell) in snapshot.Cells)
            if (row < rows && column < columns) grid[row, column] = cell.Value.ToString();

        var builder = new System.Text.StringBuilder();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (column > 0) builder.Append('\t');
                builder.Append(Quoted(grid[row, column] ?? string.Empty));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string Quoted(string value) =>
        value.IndexOfAny(['\t', '\n', '\r']) >= 0 || value.StartsWith('"')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    public ClipboardSnapshot Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var rows = new List<IReadOnlyList<string?>>();
        var row = new List<string?>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');

        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (quoted)
            {
                if (character != '"') field.Append(character);
                else if (index + 1 < normalized.Length && normalized[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = false;
                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case '\t':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        row.Add(field.ToString());
        rows.Add(row);
        return FromGrid(rows);
    }

    public ClipboardSnapshot FromGrid(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var cells = new List<(int, int, Cell)>();
        var columns = 0;
        for (var row = 0; row < rows.Count; row++)
        {
            columns = Math.Max(columns, rows[row].Count);
            for (var column = 0; column < rows[row].Count; column++)
                if (rows[row][column] is { Length: > 0 } value)
                    cells.Add((row, column, new Cell { Value = Interpret(value) }));
        }

        return new ClipboardSnapshot(rows.Count, columns, cells);
    }

    private CellValue Interpret(string text) =>
        (double.TryParse(text, System.Globalization.NumberStyles.Float, locale.Culture, out var number)
        || double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number))
        && double.IsFinite(number)
            ? CellValue.Number(number)
            : CellValue.Text(text);
}
