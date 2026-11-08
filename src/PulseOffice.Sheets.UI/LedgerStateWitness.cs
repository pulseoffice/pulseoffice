using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerStateWitness(IActiveSheetContext context) : IDocumentStateWitness
{
    public IDocumentStateMark? Mark(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Module != ModuleKind.Sheets || context.Workbook is not { } workbook) return null;
        if (context.SheetIndex < 0 || context.SheetIndex >= workbook.Sheets.Count) return null;
        var sheet = workbook.Sheets[context.SheetIndex];
        return new WorkbookMark(workbook, workbook.Protection, [.. workbook.Sheets], sheet, sheet.Protection);
    }

    private sealed class WorkbookMark(Workbook workbook, WorkbookProtection structure, IReadOnlyList<Worksheet> sheets, Worksheet sheet, SheetProtection protection) : IDocumentStateMark
    {
        public string? Broken()
        {
            if (!Equals(workbook.Protection, structure) || !Equals(sheet.Protection, protection)) return "status.dialog.stale.protection";
            return workbook.Sheets.SequenceEqual(sheets, ReferenceEqualityComparer.Instance) ? null : "status.dialog.stale.target";
        }
    }
}
