using System.Globalization;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public interface ITableEditModel
{
    string Display(object? value);

    IReadOnlyList<TableCellFormat> WithVerticalAlign(
        IReadOnlyList<TableCellFormat> formats, int firstRow, int lastRow, int firstColumn, int lastColumn, CellVerticalAlign align);

    IReadOnlyList<TableCellFormat> WithAppendedRow(IReadOnlyList<TableCellFormat> formats, int row);

    IReadOnlyList<TableCellFormat> WithAppendedColumn(IReadOnlyList<TableCellFormat> formats, int column);

    DataSnapshot Compose(DataSnapshot original, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows);
}

public sealed class TableEditModel : ITableEditModel
{
    public string Display(object? value) => value?.ToString() ?? string.Empty;

    public IReadOnlyList<TableCellFormat> WithVerticalAlign(
        IReadOnlyList<TableCellFormat> formats, int firstRow, int lastRow, int firstColumn, int lastColumn, CellVerticalAlign align)
    {
        ArgumentNullException.ThrowIfNull(formats);

        var result = formats.ToList();
        for (var row = firstRow; row <= lastRow; row++)
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var existing = result.FirstOrDefault(format => format.Row == row && format.Column == column);
                result.RemoveAll(format => format.Row == row && format.Column == column);
                if (align != CellVerticalAlign.Top || existing?.Look is not null)
                    result.Add((existing ?? new TableCellFormat(row, column, align)) with { VerticalAlign = align });
            }

        return result;
    }

    public IReadOnlyList<TableCellFormat> WithAppendedRow(IReadOnlyList<TableCellFormat> formats, int row)
    {
        ArgumentNullException.ThrowIfNull(formats);
        return row <= 0 ? formats : [.. formats, .. formats.Where(format => format.Row == row - 1).Select(format => format with { Row = row })];
    }

    public IReadOnlyList<TableCellFormat> WithAppendedColumn(IReadOnlyList<TableCellFormat> formats, int column)
    {
        ArgumentNullException.ThrowIfNull(formats);
        return column <= 0 ? formats : [.. formats, .. formats.Where(format => format.Column == column - 1).Select(format => format with { Column = column })];
    }

    public DataSnapshot Compose(DataSnapshot original, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var values = rows.Select(_ => new object?[headers.Count]).ToArray();
        var columns = new DataColumn[headers.Count];

        for (var column = 0; column < headers.Count; column++)
        {
            var kind = column < original.Columns.Count ? original.Columns[column].Kind : DataValueKind.Text;
            var fits = true;

            for (var row = 0; row < rows.Count; row++)
            {
                var text = column < rows[row].Count ? rows[row][column] : string.Empty;

                if (Kept(original, row, column, text, out var kept))
                {
                    values[row][column] = kept;
                    continue;
                }

                if (Parsed(text, kind) is { } parsed) values[row][column] = parsed;
                else
                {
                    values[row][column] = kind == DataValueKind.Text ? text : text.Length == 0 ? null : text;
                    fits &= kind == DataValueKind.Text || text.Length == 0;
                }
            }

            columns[column] = new DataColumn(headers[column], fits ? kind : DataValueKind.Text);
        }

        return original with
        {
            Columns = columns,
            Rows = [.. values.Select(line => (IReadOnlyList<object?>)line)],
        };
    }

    private bool Kept(DataSnapshot original, int row, int column, string text, out object? value)
    {
        value = null;
        if (row >= original.Rows.Count || column >= original.Columns.Count) return false;

        var line = original.Rows[row];
        value = column < line.Count ? line[column] : null;
        return Display(value) == text;
    }

    private object? Parsed(string text, DataValueKind kind) => kind switch
    {
        DataValueKind.Number when double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var number) => number,
        DataValueKind.Date when DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var moment) => moment,
        DataValueKind.Boolean when bool.TryParse(text, out var flag) => flag,
        _ => null,
    };
}
