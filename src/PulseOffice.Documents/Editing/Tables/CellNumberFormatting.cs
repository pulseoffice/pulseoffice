using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing.Tables;

public interface ICellValueText
{
    bool TryValue(string text, CellNumberFormat? format, CultureInfo culture, out double value);

    string Format(double value, CellNumberFormat format, CultureInfo culture);
}

public sealed class CellValueText : ICellValueText
{
    private const double MinimumDate = -657434;

    private const double MaximumDate = 2958465;

    public bool TryValue(string text, CellNumberFormat? format, CultureInfo culture, out double value)
    {
        value = 0;
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return false;
        if (format?.Kind == CellValueKind.Date)
        {
            if (!DateTime.TryParse(trimmed, culture, DateTimeStyles.None, out var date)) return double.TryParse(trimmed, NumberStyles.Float, culture, out value) && double.IsFinite(value);
            value = date.ToOADate();
            return true;
        }
        var percent = trimmed.EndsWith('%');
        var symbol = format?.Symbol ?? culture.NumberFormat.CurrencySymbol;
        var unmarked = trimmed.TrimEnd('%');
        var plain = (symbol.Length == 0 ? unmarked : unmarked.Replace(symbol, string.Empty, StringComparison.Ordinal)).Replace(' ', ' ').Trim();
        if (!double.TryParse(plain, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, culture, out value) || !double.IsFinite(value)) return false;
        if (percent) value /= 100;
        return true;
    }

    public string Format(double value, CellNumberFormat format, CultureInfo culture)
    {
        var decimals = Math.Clamp(format.Decimals, 0, 10);
        var pattern = (format.Grouping ? "#,##0" : "0") + (decimals > 0 ? "." + new string('0', decimals) : string.Empty);
        return format.Kind switch
        {
            CellValueKind.Percent => (value * 100).ToString(pattern, culture) + culture.NumberFormat.PercentSymbol,
            CellValueKind.Currency => Currency(value, pattern, format, culture),
            CellValueKind.Date when value is >= MinimumDate and <= MaximumDate => DateTime.FromOADate(value).ToString(format.LongDate ? "D" : "d", culture),
            CellValueKind.Date => value.ToString(pattern, culture),
            _ => value.ToString(pattern, culture),
        };
    }

    private string Currency(double value, string pattern, CellNumberFormat format, CultureInfo culture)
    {
        var symbol = format.Symbol ?? culture.NumberFormat.CurrencySymbol;
        var amount = Math.Abs(value).ToString(pattern, culture);
        var text = culture.NumberFormat.CurrencyPositivePattern is 0 or 2 ? symbol + (culture.NumberFormat.CurrencyPositivePattern == 2 ? " " : string.Empty) + amount : amount + (culture.NumberFormat.CurrencyPositivePattern == 3 ? " " : string.Empty) + symbol;
        return value < 0 ? "-" + text : text;
    }
}

public interface ICellNumberFormatting
{
    int Apply(TextDocument document, UndoStack history, TableBlock table, TableCellRange range, CellNumberFormat? format, CultureInfo culture);

    bool Recognise(TextDocument document, UndoStack history, TableCell cell, CultureInfo culture);

    int Protect(TextDocument document, UndoStack history, TableBlock table, TableCellRange range, bool on);
}

public sealed class CellNumberFormatting(IDocumentEditor editor, ICellValueText values) : ICellNumberFormatting
{
    public int Apply(TextDocument document, UndoStack history, TableBlock table, TableCellRange range, CellNumberFormat? format, CultureInfo culture)
    {
        var changed = 0;
        using (history.BeginTransaction("cell-number-format"))
        {
            var recorder = new EditRecorder(document, "cell-number-format").Touch(table);
            foreach (var cell in Cells(table, range)) cell.NumberFormat = format;
            recorder.Commit(history);
            foreach (var cell in Cells(table, range))
                if (Recognise(document, history, cell, culture)) changed++;
        }
        return changed;
    }

    public bool Recognise(TextDocument document, UndoStack history, TableCell cell, CultureInfo culture)
    {
        if (cell.NumberFormat is not { } format || cell.Formula is not null || cell.Content.Blocks is not [ParagraphBlock paragraph]) return false;
        if (!values.TryValue(paragraph.Text, format, culture, out var value)) return false;
        var text = values.Format(value, format, culture);
        if (text == paragraph.Text) return false;
        if (paragraph.Length > 0) editor.Delete(document, history, DocumentRange.In(paragraph.Id, 0, paragraph.Length));
        editor.InsertText(document, history, new DocumentPosition(paragraph.Id, 0), text);
        return true;
    }

    public int Protect(TextDocument document, UndoStack history, TableBlock table, TableCellRange range, bool on)
    {
        var recorder = new EditRecorder(document, on ? "protect-cells" : "unprotect-cells").Touch(table);
        var count = 0;
        foreach (var cell in Cells(table, range).Where(cell => cell.Protected != on))
        {
            cell.Protected = on;
            count++;
        }
        recorder.Commit(history);
        return count;
    }

    private IEnumerable<TableCell> Cells(TableBlock table, TableCellRange range)
    {
        for (var row = range.Top; row <= range.Bottom && row < table.Rows.Count; row++)
            for (var column = range.Left; column <= range.Right && column < table.Rows[row].Cells.Count; column++)
                if (!table.Rows[row].Cells[column].Covered) yield return table.Rows[row].Cells[column];
    }
}
