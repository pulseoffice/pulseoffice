using System.Globalization;
using PulseOffice.Document.Tables;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTableLooks
{
    private const int FirstRowFlag = 0x0020;
    private const int LastRowFlag = 0x0040;
    private const int FirstColumnFlag = 0x0080;
    private const int LastColumnFlag = 0x0100;
    private const int NoHorizontalBandFlag = 0x0200;
    private const int NoVerticalBandFlag = 0x0400;

    public W.TableLook Write(bool headerRow, TableBanding banding)
    {
        ArgumentNullException.ThrowIfNull(banding);
        var flags = (headerRow ? FirstRowFlag : 0)
            | (banding.LastRow ? LastRowFlag : 0)
            | (banding.FirstColumn ? FirstColumnFlag : 0)
            | (banding.LastColumn ? LastColumnFlag : 0)
            | (banding.BandedRows ? 0 : NoHorizontalBandFlag)
            | (banding.BandedColumns ? 0 : NoVerticalBandFlag);
        return new W.TableLook
        {
            Val = flags.ToString("X4", CultureInfo.InvariantCulture),
            FirstRow = headerRow,
            LastRow = banding.LastRow,
            FirstColumn = banding.FirstColumn,
            LastColumn = banding.LastColumn,
            NoHorizontalBand = !banding.BandedRows,
            NoVerticalBand = !banding.BandedColumns,
        };
    }

    public TableBanding Read(W.TableLook? look)
    {
        if (look is null) return TableBanding.Standard;
        var flags = Flags(look);
        return new TableBanding
        {
            BandedRows = !(look.NoHorizontalBand?.Value ?? (flags & NoHorizontalBandFlag) != 0),
            BandedColumns = !(look.NoVerticalBand?.Value ?? (flags & NoVerticalBandFlag) != 0),
            FirstColumn = look.FirstColumn?.Value ?? (flags & FirstColumnFlag) != 0,
            LastColumn = look.LastColumn?.Value ?? (flags & LastColumnFlag) != 0,
            LastRow = look.LastRow?.Value ?? (flags & LastRowFlag) != 0,
        };
    }

    public bool Headed(W.TableLook? look) =>
        look is not null && (look.FirstRow?.Value ?? (Flags(look) & FirstRowFlag) != 0);

    private int Flags(W.TableLook look) =>
        int.TryParse(look.Val?.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags) ? flags : 0;
}
