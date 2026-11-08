namespace PulseOffice.Sheets.Engine.Localization;

public sealed record FormulaPunctuation(char Argument, char ArrayColumn, char ArrayRow, char Decimal)
{
    public bool ArrayRowIsSemicolon => ArrayRow == ';';
}

public interface IFormulaPunctuationConvention
{
    FormulaPunctuation Canonical { get; }

    FormulaPunctuation For(string language);

    bool IsEnglish(string language);
}

public sealed class FormulaPunctuationConvention : IFormulaPunctuationConvention
{
    private readonly FormulaPunctuation _english = new(',', ',', ';', '.');
    private readonly FormulaPunctuation _continental = new(';', ';', '|', ',');
    private readonly IReadOnlySet<string> _continentalLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "it", "de", "es", "fr" };

    public FormulaPunctuation Canonical { get; } = new(';', ';', '|', '.');

    public FormulaPunctuation For(string language) => IsEnglish(language) ? _english : _continental;

    public bool IsEnglish(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var primary = language.Split('-', '_')[0];
        return !_continentalLanguages.Contains(primary);
    }
}
