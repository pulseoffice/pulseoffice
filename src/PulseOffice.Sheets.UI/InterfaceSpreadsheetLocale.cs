using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.UI;

public sealed class InterfaceSpreadsheetLocale(IInterfaceCulture culture) : ISpreadsheetLocale
{
    public InterfaceSpreadsheetLocale(ITextCatalog texts, Func<CultureInfo> system) : this(new InterfaceCulture(texts, system))
    {
    }

    public CultureInfo Culture => culture.Culture;
}
