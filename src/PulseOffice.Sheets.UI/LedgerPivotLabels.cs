using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Sheets.Pivot;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPivotLabels(ITextCatalog texts)
{
    public PivotLabels Create() => new(texts["pivot.grandTotal"], texts["pivot.subtotal"], texts["pivot.blank"])
    {
        Quarter = texts["pivot.quarter"],
        Culture = CultureOf(texts.Language),
    };

    private CultureInfo CultureOf(UiLanguage language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language.ToCode());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
