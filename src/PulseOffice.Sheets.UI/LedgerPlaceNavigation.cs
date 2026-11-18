namespace PulseOffice.Sheets.UI;

public interface ILedgerPlaceNavigation
{
    bool GoTo(IActiveSheetContext context, string place);
}

public sealed class LedgerPlaceNavigation(INameBoxResolver resolver) : ILedgerPlaceNavigation
{
    public bool GoTo(IActiveSheetContext context, string place)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Workbook is not { } workbook || string.IsNullOrEmpty(place)) return false;
        var reference = place.TrimStart('#');
        if (reference.Length == 0) return false;
        if ((resolver.Resolve(reference, workbook, context.SheetIndex) ?? Dotted(reference, workbook, context.SheetIndex)) is not { } target) return false;
        if (target.SheetIndex != context.SheetIndex) context.SelectSheet(target.SheetIndex);
        context.SelectRange(target.Range);
        return true;
    }

    private NameBoxTarget? Dotted(string reference, Workbook workbook, int sheetIndex)
    {
        var dot = reference.LastIndexOf('.');
        return dot > 0 && !reference.Contains('!', StringComparison.Ordinal)
            ? resolver.Resolve(reference[..dot] + "!" + reference[(dot + 1)..], workbook, sheetIndex)
            : null;
    }
}
