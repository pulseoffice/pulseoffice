using System.Globalization;

namespace PulseOffice.Sheets.Formatting;

public interface IBooleanNames
{
    string Name(bool value);
}

public sealed class CultureBooleanNames(CultureInfo culture) : IBooleanNames
{
    private static readonly Dictionary<string, (string True, string False)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = ("TRUE", "FALSE"),
        ["it"] = ("VERO", "FALSO"),
        ["de"] = ("WAHR", "FALSCH"),
        ["fr"] = ("VRAI", "FAUX"),
        ["es"] = ("VERDADERO", "FALSO"),
    };

    public string Name(bool value)
    {
        var names = Names.GetValueOrDefault(culture.TwoLetterISOLanguageName, Names["en"]);
        return value ? names.True : names.False;
    }
}
