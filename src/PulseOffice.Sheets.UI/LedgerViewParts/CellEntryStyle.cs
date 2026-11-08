using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

public interface ICellEntryStyle
{
    int? StyleFor(Workbook workbook, Worksheet sheet, CellAddress address, string? recognizedFormatCode);
}

public sealed class CellEntryStyle : ICellEntryStyle
{
    public int? StyleFor(Workbook workbook, Worksheet sheet, CellAddress address, string? recognizedFormatCode)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        if (recognizedFormatCode is not { } code) return null;

        var current = sheet.StyleIdAt(address);
        if (workbook.Styles.Get(current).NumberFormatId != 0) return null;

        var formatId = workbook.NumberFormats.Register(code);
        return workbook.Styles.Derive(current, style => style with { NumberFormatId = formatId });
    }
}
