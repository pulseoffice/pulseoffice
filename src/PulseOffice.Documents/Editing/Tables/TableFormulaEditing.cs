using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITableFormulaEditing
{
    bool IsValid(string formula);

    DocumentEdit SetFormula(TextDocument document, UndoStack history, TableCellLocation at, string? formula, CultureInfo culture);

    bool Recalculate(TextDocument document, UndoStack history, TableBlock table, CultureInfo culture);

    double? Evaluate(TableBlock table, int row, int column, string formula, CultureInfo culture);

    string Format(double value, CultureInfo culture);
}

public sealed class TableFormulaEditing(ITableFormulaParser parser, ITableGrid grid, IBlockTree tree, IEnumerable<ITableFormulaSyntax> syntaxes) : ITableFormulaEditing
{
    private readonly ITableFormulaSyntax _canonical = syntaxes.First(syntax => syntax.Name == "canonical");

    private sealed class Context(TableFormulaEditing owner, TableBlock table, int row, int column, CultureInfo culture) : ITableFormulaContext
    {
        public int Row => row;

        public int Column => column;

        public int RowCount => table.Rows.Count;

        public int ColumnCount => table.ColumnCount;

        public double? Value(int r, int c)
        {
            if (r < 0 || r >= table.Rows.Count || c < 0 || c >= table.Rows[r].Cells.Count || r == row && c == column) return null;
            var text = owner.Text(table.Rows[r].Cells[c]).Trim();
            if (text.Length == 0) return null;
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var value)) return value;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant) ? invariant : null;
        }
    }

    private string Text(TableCell cell) => grid.PlainText(cell);

    public bool IsValid(string formula) => parser.TryParse(formula, out _);

    public double? Evaluate(TableBlock table, int row, int column, string formula, CultureInfo culture) =>
        parser.TryParse(formula, out var node) && node is not null ? node.Evaluate(new Context(this, table, row, column, culture)) : null;

    public string Format(double value, CultureInfo culture) =>
        double.IsNaN(value) || double.IsInfinity(value) ? "#DIV/0!" : value.ToString("#,##0.##########", culture);

    public DocumentEdit SetFormula(TextDocument document, UndoStack history, TableCellLocation at, string? formula, CultureInfo culture)
    {
        using var transaction = history.BeginTransaction("table-formula");
        var cell = at.Table.Cell(at.Row, at.Column);
        var recorder = new EditRecorder(document, "table-formula").Touch(at.Table);
        cell.Formula = string.IsNullOrWhiteSpace(formula) ? null : Canonical(formula);
        var edit = recorder.Commit(history);
        Recalculate(document, history, at.Table, culture);
        return edit;
    }

    public bool Recalculate(TextDocument document, UndoStack history, TableBlock table, CultureInfo culture)
    {
        var changed = false;
        for (var pass = 0; pass < 4; pass++)
        {
            var round = false;
            for (var r = 0; r < table.Rows.Count; r++)
            {
                for (var c = 0; c < table.Rows[r].Cells.Count; c++)
                {
                    var cell = table.Rows[r].Cells[c];
                    if (cell.Covered || cell.Formula is not { } formula || Evaluate(table, r, c, formula, culture) is not { } value) continue;
                    var text = Format(value, culture);
                    if (grid.PlainText(cell) == text) continue;
                    var paragraph = tree.Paragraphs(cell.Content).FirstOrDefault();
                    if (paragraph is null) continue;
                    var recorder = new EditRecorder(document, "table-formula-result").Touch(cell.Content).Touch(paragraph);
                    var run = paragraph.Content.Runs.Count > 0 ? paragraph.Content.Runs[0] with { Text = text } : new TextRun(text);
                    paragraph.Content = paragraph.Content.WithRuns([run]);
                    paragraph.Marks = [];
                    var extra = tree.Paragraphs(cell.Content).Skip(1).ToList();
                    foreach (var other in extra) cell.Content.Remove(other);
                    recorder.Commit(history);
                    round = true;
                    changed = true;
                }
            }
            if (!round) break;
        }
        return changed;
    }

    private string Canonical(string formula) => _canonical.Print(parser.Parse(formula), null);
}
