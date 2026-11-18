using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;

namespace PulseOffice.Presentations.UI;

public interface IDeckTableLooks
{
    DataTableObject Apply(DataTableObject table, TableLook look, bool headerRow, TableBanding banding);
}

public sealed class DeckTableLooks : IDeckTableLooks
{
    public DataTableObject Apply(DataTableObject table, TableLook look, bool headerRow, TableBanding banding)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(banding);
        var style = table.Style.WithBanding(banding);
        var rows = table.Snapshot.Rows.Count;
        var columns = table.Snapshot.Columns.Count;
        var line = new TableBorderLine(Hex(look.BorderColor), look.BorderWidth);
        var first = headerRow ? TableCellFormat.HeaderRow : 0;
        var formats = new List<TableCellFormat>();
        for (var row = first; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var header = row < 0;
                var fill = header ? look.HeaderFill
                    : banding.BandsRow(row) ? look.BandFill
                    : banding.BandsColumn(column) ? look.BandFill ?? look.BodyFill
                    : look.BodyFill;
                var opening = row == first;
                var closing = header || row == rows - 1;
                var align = table.CellFormats.FirstOrDefault(format => format.Row == row && format.Column == column)?.VerticalAlign ?? CellVerticalAlign.Center;
                formats.Add(new TableCellFormat(row, column, align)
                {
                    Look = new TableCellLook
                    {
                        FillHex = fill is { } colour ? Hex(colour) : null,
                        TextHex = header && look.HeaderInk is { } ink ? Hex(ink) : null,
                        Bold = header ? look.HeaderBold : banding.Emphasizes(row, column, rows, columns),
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
            Style = style with
            {
                HeaderRow = headerRow,
                BandedRows = banding.BandedRows && look.BandFill is not null,
                Grid = look.InnerLines,
                AccentHex = Hex(look.HeaderFill ?? look.BorderColor),
                OwnLook = true,
            },
        };
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}
