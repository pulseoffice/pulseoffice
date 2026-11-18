using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public interface ILedgerTableLooks
{
    void ApplyToSelection(string description, TableLook look, bool headerRow, TableBanding banding);
}

public sealed class LedgerTableLooks(ISheetFormattingService formatting) : ILedgerTableLooks
{
    public void ApplyToSelection(string description, TableLook look, bool headerRow, TableBanding banding)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(banding);
        formatting.ApplyToSelection(description, (range, address, current) =>
        {
            var row = address.Row - range.Start.Row;
            var column = address.Column - range.Start.Column;
            var last = address.Row == range.End.Row;
            var header = headerRow && row == 0;
            var body = row - (headerRow ? 1 : 0);
            var bodyRows = range.End.Row - range.Start.Row + 1 - (headerRow ? 1 : 0);
            var columns = range.End.Column - range.Start.Column + 1;
            var emphasized = !header && banding.Emphasizes(body, column, bodyRows, columns);
            var fill = header ? look.HeaderFill
                : banding.BandsRow(body) ? look.BandFill
                : banding.BandsColumn(column) ? look.BandFill ?? look.BodyFill
                : look.BodyFill;
            return current with
            {
                Font = current.Font with
                {
                    Bold = header && look.HeaderBold || emphasized,
                    ColorHex = header && look.HeaderInk is { } ink ? Hex(ink) : current.Font.ColorHex,
                },
                BackgroundHex = fill is { } color ? Hex(color) : null,
                Borders = Borders(look, row == 0, header || last, !header && banding.LastRow && last),
            };
        });
    }

    private CellBorders Borders(TableLook look, bool first, bool closing, bool total)
    {
        var edge = new BorderEdge(look.BorderWidth >= 1 ? BorderStyle.Medium : BorderStyle.Thin, Hex(look.BorderColor), Foundation.Units.Twip.FromPoints(look.BorderWidth));
        return look.InnerLines
            ? new CellBorders(edge, edge, edge, edge)
            : new CellBorders(BorderEdge.None, first || total ? edge : BorderEdge.None, BorderEdge.None, closing ? edge : BorderEdge.None);
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}
