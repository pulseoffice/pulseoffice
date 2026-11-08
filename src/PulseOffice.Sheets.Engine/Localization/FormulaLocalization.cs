using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Localization;

public interface IFormulaLocalization
{
    string ToCanonical(string typed, string language);

    string ToDisplay(string canonical, string language);

    FormulaPunctuation Punctuation(string language);

    string FunctionName(string canonicalName, string language);
}

public sealed class FormulaLocalization(
    IFormulaExpressionParser parser,
    IFunctionNameVocabulary vocabulary,
    IFormulaPunctuationConvention punctuation,
    IFormulaTextTranslator translator) : IFormulaLocalization
{
    public FormulaLocalization(IFormulaExpressionParser parser, IFunctionNameVocabulary vocabulary)
        : this(parser, vocabulary, new FormulaPunctuationConvention(), new FormulaTextTranslator()) { }

    public FormulaPunctuation Punctuation(string language) => punctuation.For(Language(language));

    public string FunctionName(string canonicalName, string language) => vocabulary.ToLanguage(canonicalName, Language(language));

    public string ToCanonical(string typed, string language)
    {
        ArgumentNullException.ThrowIfNull(typed);
        if (!typed.StartsWith('=')) return typed;
        var local = Language(language);
        var canonical = translator.ToCanonical(typed, punctuation.For(local), name => vocabulary.ToCanonical(name, local).ToUpperInvariant(), name => CanonicalLogical(name, local));
        try
        {
            parser.Parse(canonical);
            return canonical;
        }
        catch (FormulaSyntaxException)
        {
            return typed;
        }
    }

    public string ToDisplay(string canonical, string language)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        if (!canonical.StartsWith('=')) return canonical;
        var local = Language(language);
        return translator.ToLocal(canonical, punctuation.For(local), name => local == "en" ? name : vocabulary.ToLanguage(name, local), name => LocalLogical(name, local));
    }

    private string CanonicalLogical(string name, string language)
    {
        var canonical = vocabulary.ToCanonical(name.ToUpperInvariant(), language).ToUpperInvariant();
        return IsLogical(canonical) ? canonical : name;
    }

    private string LocalLogical(string name, string language) =>
        language != "en" && IsLogical(name.ToUpperInvariant()) ? vocabulary.ToLanguage(name.ToUpperInvariant(), language) : name;

    private bool IsLogical(string canonicalName) => canonicalName is "TRUE" or "FALSE";

    private string Language(string language) =>
        vocabulary.SupportedLanguages.Contains(language) ? language : "en";
}
