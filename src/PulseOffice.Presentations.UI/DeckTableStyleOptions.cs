using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.UI.Controls.Tables;

namespace PulseOffice.Presentations.UI;

public interface IDeckTableStyleOptions
{
    TableLookOptionsChoice Of(DataTableObject table);

    DataTableObject Apply(DataTableObject table, TableLookOptionsChoice choice);
}

public sealed class DeckTableStyleOptions : IDeckTableStyleOptions
{
    private const string BandHex = "#F3F4F6";

    public TableLookOptionsChoice Of(DataTableObject table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return new TableLookOptionsChoice(table.Style.HeaderRow, table.Style.ToBanding());
    }

    public DataTableObject Apply(DataTableObject table, TableLookOptionsChoice choice)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(choice);
        var style = table.Style.WithBanding(choice.Banding) with { HeaderRow = choice.HeaderRow };
        if (!table.Style.OwnLook) return table with { Style = style };

        var before = table.Style.ToBanding();
        var after = choice.Banding;
        var rows = table.Snapshot.Rows.Count;
        var columns = table.Snapshot.Columns.Count;
        var formats = table.CellFormats.Select(format =>
        {
            if (format.Look is not { } look || format.Row < 0) return format;
            var wasEmphasized = before.Emphasizes(format.Row, format.Column, rows, columns);
            var isEmphasized = after.Emphasizes(format.Row, format.Column, rows, columns);
            var bold = wasEmphasized == isEmphasized ? look.Bold : isEmphasized;
            var wasBanded = Banded(before, format.Row, format.Column);
            var isBanded = Banded(after, format.Row, format.Column);
            var fill = wasBanded == isBanded ? look.FillHex
                : isBanded ? look.FillHex ?? BandHex
                : look.FillHex == BandHex ? null : look.FillHex;
            return format with { Look = look with { Bold = bold, FillHex = fill } };
        }).ToList();
        return table with { Style = style, CellFormats = formats };
    }

    private bool Banded(TableBanding banding, int row, int column) => banding.BandsRow(row) || banding.BandsColumn(column);
}
