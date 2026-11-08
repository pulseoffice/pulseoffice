using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.Scripting;

public interface IScriptProtection
{
    void Cells(CellRange range);

    void Format();

    void Permitted(Func<SheetProtection, bool> permission, string action);

    void Unprotected(string action);

    void Structure();
}

public sealed class ScriptProtection(IActiveSheetContext context, ISheetEditGuard guard) : IScriptProtection
{
    public void Cells(CellRange range)
    {
        if (context.Workbook is not { } workbook) return;
        if (!guard.CanWrite(workbook, Sheet(workbook), range))
            throw new InvalidOperationException(range.IsSingleCell
                ? $"The cell {range.Start.ToA1()} is protected: unprotect the sheet first."
                : $"The cells {range} are protected: unprotect the sheet first.");
    }

    public void Format()
    {
        if (context.Workbook is { } workbook && !guard.CanFormat(Sheet(workbook)))
            throw new InvalidOperationException("The sheet is protected against formatting: unprotect the sheet first.");
    }

    public void Permitted(Func<SheetProtection, bool> permission, string action)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (context.Workbook is { } workbook && Sheet(workbook).Protection is { IsEnabled: true } protection && !permission(protection))
            throw new InvalidOperationException($"The sheet is protected: {action} is not allowed. Unprotect the sheet first.");
    }

    public void Unprotected(string action) => Permitted(_ => false, action);

    public void Structure()
    {
        if (context.Workbook is { Protection.Structure: true })
            throw new InvalidOperationException("The structure of the workbook is protected: sheets cannot be added, removed or renamed.");
    }

    private Worksheet Sheet(Workbook workbook) => workbook.Sheets[context.SheetIndex];
}
