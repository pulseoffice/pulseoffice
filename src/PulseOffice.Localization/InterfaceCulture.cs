using System.Globalization;

namespace PulseOffice.Localization;

public interface IInterfaceCulture
{
    CultureInfo Culture { get; }
}

public sealed class InterfaceCulture(ITextCatalog texts, Func<CultureInfo> system) : IInterfaceCulture
{
    private readonly IReadOnlyDictionary<UiLanguage, string> _regions = new Dictionary<UiLanguage, string>
    {
        [UiLanguage.Italian] = "it-IT",
        [UiLanguage.English] = "en-US",
        [UiLanguage.German] = "de-DE",
        [UiLanguage.Spanish] = "es-ES",
        [UiLanguage.French] = "fr-FR",
    };

    public InterfaceCulture(ITextCatalog texts) : this(texts, () => CultureInfo.CurrentCulture)
    {
    }

    public CultureInfo Culture
    {
        get
        {
            var language = texts.Language;
            var own = system();
            if (string.Equals(own.TwoLetterISOLanguageName, language.ToCode(), StringComparison.OrdinalIgnoreCase)) return own;
            return CultureInfo.GetCultureInfo(_regions.TryGetValue(language, out var region) ? region : "en-US");
        }
    }
}
