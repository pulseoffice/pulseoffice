using PulseOffice.Typography.Embedding;

namespace PulseOffice.Sheets.UI.Fonts;

public sealed class LedgerFontCollector(IWorkbookFontNames names) : IUsedFontCollector
{
    public bool Handles(object model) => model is Workbook;

    public IReadOnlyCollection<string> Families(object model) => model is Workbook workbook ? names.Used(workbook) : [];
}
