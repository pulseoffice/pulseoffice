using System.Globalization;

namespace PulseOffice.Localization;

public enum UiLanguage
{
    System,
    Italian,
    English,
    German,
    Spanish,
    French,
}

public static class UiLanguages
{
    public static IReadOnlyList<UiLanguage> Translated { get; } =
[
    UiLanguage.Italian, UiLanguage.English, UiLanguage.German,
        UiLanguage.Spanish, UiLanguage.French,
    ];

    public static string ToCode(this UiLanguage language) => language switch
    {
        UiLanguage.Italian => "it",
        UiLanguage.German => "de",
        UiLanguage.Spanish => "es",
        UiLanguage.French => "fr",
        _ => "en",
    };

    public static string NativeName(this UiLanguage language) => language switch
    {
        UiLanguage.Italian => "Italiano",
        UiLanguage.English => "English",
        UiLanguage.German => "Deutsch",
        UiLanguage.Spanish => "Español",
        UiLanguage.French => "Français",
        _ => "System",
    };

    public static UiLanguage FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return culture.TwoLetterISOLanguageName switch
        {
            "it" => UiLanguage.Italian,
            "de" => UiLanguage.German,
            "es" => UiLanguage.Spanish,
            "fr" => UiLanguage.French,
            _ => UiLanguage.English,
        };
    }

    public static UiLanguage Resolve(this UiLanguage language) =>
    language == UiLanguage.System
        ? FromCulture(CultureInfo.CurrentUICulture)
        : language;
}
