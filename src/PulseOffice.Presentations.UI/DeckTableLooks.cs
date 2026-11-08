using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;

namespace PulseOffice.Presentations.UI;

public interface IDeckTableLooks
{
    DataTableObject Apply(DataTableObject table, TableLook look, bool headerRow, bool bandedRows);
}

public sealed class DeckTableLooks : IDeckTableLooks
{
    public DataTableObject Apply(DataTableObject table, TableLook look, bool headerRow, bool bandedRows)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(look);
        var rows = table.Snapshot.Rows.Count;
        var columns = table.Snapshot.Columns.Count;
        var line = new TableBorderLine(Hex(look.BorderColor), look.BorderWidth);
        var first = headerRow ? TableCellFormat.HeaderRow : 0;
        var formats = new List<TableCellFormat>();
        for (var row = first; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var header = row < 0;
                var fill = header ? look.HeaderFill : bandedRows && row % 2 == 1 ? look.BandFill : look.BodyFill;
                var opening = row == first;
                var closing = header || row == rows - 1;
                var align = table.CellFormats.FirstOrDefault(format => format.Row == row && format.Column == column)?.VerticalAlign ?? CellVerticalAlign.Center;
                formats.Add(new TableCellFormat(row, column, align)
                {
                    Look = new TableCellLook
                    {
                        FillHex = fill is { } colour ? Hex(colour) : null,
                        TextHex = header && look.HeaderInk is { } ink ? Hex(ink) : null,
                        Bold = header && look.HeaderBold,
                        Top = look.InnerLines || opening ? line : null,
                        Bottom = look.InnerLines || closing ? line : null,
                        Left = look.InnerLines ? line : null,
                        Right = look.InnerLines ? line : null,
                    },
                });
            }

        return table with
        {
            CellFormats = formats,
            Style = table.Style with
            {
                HeaderRow = headerRow,
                BandedRows = bandedRows && look.BandFill is not null,
                Grid = look.InnerLines,
                AccentHex = Hex(look.HeaderFill ?? look.BorderColor),
                OwnLook = true,
            },
        };
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}
