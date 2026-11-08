using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public interface ILedgerTableLooks
{
    void ApplyToSelection(string description, TableLook look, bool headerRow, bool bandedRows);
}

public sealed class LedgerTableLooks(ISheetFormattingService formatting) : ILedgerTableLooks
{
    public void ApplyToSelection(string description, TableLook look, bool headerRow, bool bandedRows)
    {
        ArgumentNullException.ThrowIfNull(look);
        formatting.ApplyToSelection(description, (range, address, current) =>
        {
            var row = address.Row - range.Start.Row;
            var last = address.Row == range.End.Row;
            var header = headerRow && row == 0;
            var body = row - (headerRow ? 1 : 0);
            var fill = header ? look.HeaderFill : bandedRows && body % 2 == 1 ? look.BandFill : look.BodyFill;
            return current with
            {
                Font = current.Font with
                {
                    Bold = header && look.HeaderBold,
                    ColorHex = header && look.HeaderInk is { } ink ? Hex(ink) : current.Font.ColorHex,
                },
                BackgroundHex = fill is { } color ? Hex(color) : null,
                Borders = Borders(look, row == 0, header || last),
            };
        });
    }

    private CellBorders Borders(TableLook look, bool first, bool closing)
    {
        var edge = new BorderEdge(look.BorderWidth >= 1 ? BorderStyle.Medium : BorderStyle.Thin, Hex(look.BorderColor), Foundation.Units.Twip.FromPoints(look.BorderWidth));
        return look.InnerLines
            ? new CellBorders(edge, edge, edge, edge)
            : new CellBorders(BorderEdge.None, first ? edge : BorderEdge.None, BorderEdge.None, closing ? edge : BorderEdge.None);
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}
