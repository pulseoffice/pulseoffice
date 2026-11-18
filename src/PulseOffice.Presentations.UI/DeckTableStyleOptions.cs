using PulseOffice.Document.Embedding;

namespace PulseOffice.Presentations.UI;

public sealed record DeckTableEmphasis(bool FirstColumn, bool LastColumn, bool LastRow, bool BandedColumns);

public interface IDeckTableStyleOptions
{
    DeckTableEmphasis Of(DataTableObject table);

    DataTableObject Apply(DataTableObject table, DeckTableEmphasis emphasis);
}

public sealed class DeckTableStyleOptions : IDeckTableStyleOptions
{
    private const string BandHex = "#F3F4F6";

    public DeckTableEmphasis Of(DataTableObject table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return new DeckTableEmphasis(table.Style.FirstColumn, table.Style.LastColumn, table.Style.LastRow, table.Style.BandedColumns);
    }

    public DataTableObject Apply(DataTableObject table, DeckTableEmphasis emphasis)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(emphasis);
        var style = table.Style with
        {
            FirstColumn = emphasis.FirstColumn,
            LastColumn = emphasis.LastColumn,
            LastRow = emphasis.LastRow,
            BandedColumns = emphasis.BandedColumns,
        };
        if (!table.Style.OwnLook) return table with { Style = style };

        var before = Of(table);
        var rows = table.Snapshot.Rows.Count;
        var columns = table.Snapshot.Columns.Count;
        var formats = table.CellFormats.Select(format =>
        {
            if (format.Look is not { } look || format.Row < 0) return format;
            var wasEmphasized = Emphasized(before, format.Row, format.Column, rows, columns);
            var isEmphasized = Emphasized(emphasis, format.Row, format.Column, rows, columns);
            var bold = wasEmphasized == isEmphasized ? look.Bold : isEmphasized;
            var banded = format.Column % 2 == 1 && !(table.Style.BandedRows && format.Row % 2 == 1);
            var fill = !banded || before.BandedColumns == emphasis.BandedColumns ? look.FillHex
                : emphasis.BandedColumns ? look.FillHex ?? BandHex
                : look.FillHex == BandHex ? null : look.FillHex;
            return format with { Look = look with { Bold = bold, FillHex = fill } };
        }).ToList();
        return table with { Style = style, CellFormats = formats };
    }

    private bool Emphasized(DeckTableEmphasis emphasis, int row, int column, int rows, int columns) =>
        (emphasis.FirstColumn && column == 0)
        || (emphasis.LastColumn && column == columns - 1)
        || (emphasis.LastRow && row == rows - 1);
}
